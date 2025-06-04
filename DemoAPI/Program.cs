using DemoAPI.OrderSaga;
using Microsoft.EntityFrameworkCore;
using OrchestratR.Recovery;
using OrchestratR.Registration;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using DemoAPI.OrderSagaSync;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();


builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("DemoAPI"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("OrchestratR")
        //.AddConsoleExporter()
        );

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<IInventoryServiceSync, InventoryServiceSync>();
builder.Services.AddTransient<IPaymentServiceSync, PaymentServiceSync>();
builder.Services.AddTransient<IShippingServiceSync, ShippingServiceSync>();

builder.Services.AddSagaInfrastructure(options =>
{
    options
        .UseEfCore(db => db.UseSqlServer(
            "Server=DESKTOP-NBMOCEP\\SQLEXPRESS01;" +
            "Database=SagaDb;Trusted_Connection=True;Encrypt=False",
            x => x.MigrationsAssembly(SagaInfrastructureOptions.GetMigrationsAssembly())))
        .UseTracing();
});

builder.Services.AddSaga<OrderSagaContext>()
                .WithStep<ReserveInventoryStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(3))
                .WithStep<ProcessPaymentStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(3))
                .WithStep<ShipOrderStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(3))
                .Build();

builder.Services.AddSaga<OrderSagaSyncContext>()
    .WithStep<ReserveInventorySyncStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(2))
    .WithStep<ProcessPaymentSyncStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(2))
    .WithStep<ShipOrderSyncStep>(x => x.WithTimeout(TimeSpan.FromMilliseconds(5000)).WithRetry(2))
    .Build();

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