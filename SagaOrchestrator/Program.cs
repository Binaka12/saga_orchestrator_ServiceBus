using Azure.Messaging.ServiceBus;
using Microsoft.OpenApi;
using SagaOrchestrator.Models;
using SagaOrchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SagaOrchestrator API", Version = "v1" });

    // Use fully-qualified type names (or otherwise unique IDs) to avoid duplicate schemaId collisions
    c.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", "."));
});

// Service Bus client - reads connection string from configuration: ServiceBus:ConnectionString
//var serviceBusConnection = builder.Configuration.GetValue<string>("ServiceBus:ConnectionString");
var serviceBusConnection = "https://example.com";

if (string.IsNullOrWhiteSpace(serviceBusConnection))
    throw new InvalidOperationException("ServiceBus connection string is not configured. Set ServiceBus:ConnectionString in configuration.");

builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));

// Register publisher abstraction
builder.Services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SagaOrchestrator v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();