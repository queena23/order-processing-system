using OrderWorker;

var builder = Host.CreateApplicationBuilder(args);

// Make configuration available to Worker
builder.Services.AddSingleton(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();