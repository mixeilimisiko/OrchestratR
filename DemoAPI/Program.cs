using DemoAPI.OrderSaga;
using OrchestratR.Core;
using OrchestratR.Orchestration;
using OrchestratR.Persistence;
using OrchestratR.Recovery;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<ISagaStore, InMemorySagaStore>();

builder.Services.AddSingleton<ReserveInventoryStep>();
builder.Services.AddSingleton<ProcessPaymentStep>();
builder.Services.AddSingleton<ShipOrderStep>();
builder.Services.AddSingleton<SagaOrchestrator<OrderSagaContext>>(provider =>
{
    var store = provider.GetRequiredService<ISagaStore>();
    var orch = new SagaOrchestrator<OrderSagaContext>(store);
    // Manually add steps in desired order:
    orch.AddStep(provider.GetRequiredService<ReserveInventoryStep>());
    orch.AddStep(provider.GetRequiredService<ProcessPaymentStep>());
    orch.AddStep(provider.GetRequiredService<ShipOrderStep>());
    return orch;
});

builder.Services.AddSingleton<ISagaOrchestrator>(sp => sp.GetRequiredService<SagaOrchestrator<OrderSagaContext>>());
builder.Services.AddHostedService<SagaRecoveryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
