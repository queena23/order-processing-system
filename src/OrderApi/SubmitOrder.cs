using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace OrderApi;

public class SubmitOrder
{
    private readonly ILogger<SubmitOrder> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly ServiceBusClient _serviceBusClient;

    public SubmitOrder(ILogger<SubmitOrder> logger)
    {
        _logger = logger;

        _cosmosClient = new CosmosClient(Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING"));
        _container = _cosmosClient.GetContainer(
            Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME"),
            Environment.GetEnvironmentVariable("COSMOS_CONTAINER_NAME")
        );

        _serviceBusClient = new ServiceBusClient(
            Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING"));
    }

    [Function("SubmitOrder")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        // 1. Read the request body
        var body = await new StreamReader(req.Body).ReadToEndAsync();

        // 2. Deserialize into an Order
        Order? order;
        try
        {
            order = JsonSerializer.Deserialize<Order>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return new BadRequestObjectResult("Invalid JSON.");
        }

        // 3. Validate
        if (order is null || string.IsNullOrWhiteSpace(order.CustomerId) || order.Items.Count == 0)
        {
            return new BadRequestObjectResult("CustomerId and at least one item are required.");
        }

        // 4. Calculate total
        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        // 5. Save to Cosmos DB
        await _container.CreateItemAsync(order, new PartitionKey(order.CustomerId));
        _logger.LogInformation("Order {OrderId} saved to Cosmos DB", order.Id);

        // 6. Drop into Service Bus queue
        var sender = _serviceBusClient.CreateSender(
            Environment.GetEnvironmentVariable("SERVICE_BUS_ORDERS_QUEUE"));

        var message = new ServiceBusMessage(JsonSerializer.Serialize(order))
        {
            MessageId = order.Id,
            ContentType = "application/json"
        };

        await sender.SendMessageAsync(message);
        _logger.LogInformation("Order {OrderId} enqueued to Service Bus", order.Id);

        // 7. Return success
        return new OkObjectResult(new
        {
            orderId = order.Id,
            message = "Order received and queued for processing.",
            total = order.TotalAmount
        });
    }
}