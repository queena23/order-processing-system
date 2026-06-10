using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderWorker;

var builder = Host.CreateApplicationBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetryWorkerService();

// Make configuration available to Worker
builder.Services.AddSingleton(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();