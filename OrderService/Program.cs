using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// ...
builder.Services.AddSingleton<IMessageDeduplicator, OrderService.Services.InMemoryMessageDeduplicator>();
builder.Services.AddHostedService<OrderService.Services.OrderCommandProcessor>();
// ...
var app = builder.Build();
app.MapControllers();
app.Run();
