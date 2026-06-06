using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FraudChecker;

public class FraudCheck
{
    private readonly ILogger<FraudCheck> _logger;
    private readonly ServiceBusClient _serviceBusClient;

    private static readonly HashSet<string> FlaggedCustomers = new()
    {
        "flagged-001",
        "flagged-002"
    };

    public FraudCheck(ILogger<FraudCheck> logger)
    {
        _logger = logger;
        _serviceBusClient = new ServiceBusClient(
            Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING"));
    }

    [Function(nameof(FraudCheck))]
    public async Task Run(
        [ServiceBusTrigger("%SERVICE_BUS_ORDERS_QUEUE%", Connection = "SERVICE_BUS_CONNECTION_STRING")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Fraud check started for message {MessageId}", message.MessageId);

        // 1. Deserialize the order
        Order? order;
        try
        {
            order = JsonSerializer.Deserialize<Order>(message.Body.ToString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize order message");
            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "DeserializationFailure",
                deadLetterErrorDescription: ex.Message);
            return;
        }

        if (order is null)
        {
            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "NullOrder",
                deadLetterErrorDescription: "Order was null after deserialization");
            return;
        }

        // 2. Run fraud rules
        var (passed, reason) = RunFraudRules(order);

        if (!passed)
        {
            _logger.LogWarning("Order {OrderId} failed fraud check: {Reason}", order.Id, reason);
            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "FraudDetected",
                deadLetterErrorDescription: reason);
            return;
        }

        // 3. Passed — forward to processing queue
        _logger.LogInformation("Order {OrderId} passed fraud check", order.Id);
        order.Status = "FraudCheckPassed";

        var sender = _serviceBusClient.CreateSender(
            Environment.GetEnvironmentVariable("SERVICE_BUS_PROCESSING_QUEUE"));

        var outMessage = new ServiceBusMessage(JsonSerializer.Serialize(order))
        {
            MessageId = order.Id,
            ContentType = "application/json"
        };

        await sender.SendMessageAsync(outMessage);
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("Order {OrderId} forwarded to processing queue", order.Id);
    }

    private static (bool passed, string? reason) RunFraudRules(Order order)
    {
        if (FlaggedCustomers.Contains(order.CustomerId))
            return (false, "Customer account flagged for suspicious activity");

        if (order.TotalAmount > 500 && order.Items.Count == 1)
            return (false, "High value single item order requires manual review");

        if (order.Items.Any(i => i.Quantity > 50))
            return (false, "Unusually high quantity detected");

        if (order.TotalAmount <= 0)
            return (false, "Invalid order total");

        return (true, null);
    }
}