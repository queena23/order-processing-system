using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace OrderWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly string _processingQueue;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        var connectionString = configuration["SERVICE_BUS_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("SERVICE_BUS_CONNECTION_STRING is missing");
        _processingQueue = configuration["SERVICE_BUS_PROCESSING_QUEUE"]
            ?? throw new InvalidOperationException("SERVICE_BUS_PROCESSING_QUEUE is missing");
        _serviceBusClient = new ServiceBusClient(connectionString);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderWorker started, listening to orders-processing queue...");

        var processor = _serviceBusClient.CreateProcessor(
            _processingQueue,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 4,
                AutoCompleteMessages = false
            });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);

        await processor.StopProcessingAsync();
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        Order? order;
        try
        {
            order = JsonSerializer.Deserialize<Order>(args.Message.Body.ToString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize order");
            await args.DeadLetterMessageAsync(args.Message, "DeserializationFailure", ex.Message);
            return;
        }

        if (order is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "NullOrder", "Order was null");
            return;
        }

        _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
            order.Id, order.CustomerId);

        await Task.Delay(500, args.CancellationToken);

        order.Status = "Completed";
        _logger.LogInformation("Order {OrderId} completed. Total: ${Total}",
            order.Id, order.TotalAmount);

        await args.CompleteMessageAsync(args.Message);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus error on {EntityPath}", args.EntityPath);
        return Task.CompletedTask;
    }
}