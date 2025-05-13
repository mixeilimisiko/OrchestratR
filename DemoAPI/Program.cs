using DemoAPI.OrderSaga;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Persistence;
using OrchestratR.Recovery;
using OrchestratR.Registration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSagaInfrastructure(options =>
{
    options.UseEfCore(db =>
        db.UseSqlServer("Server=DESKTOP-NBMOCEP\\SQLEXPRESS01; Database=SagaDb;Trusted_Connection=True;Encrypt=False",
                        x => x.MigrationsAssembly(SagaInfrastructureOptions.GetMigrationsAssembly())));
});

builder.Services.AddSaga<OrderSagaContext>()
                .WithStep<ReserveInventoryStep>(x =>x.WithTimeout(TimeSpan.FromSeconds(500)).WithRetry(3))
                .WithStep<ProcessPaymentStep>(x => x.WithTimeout(TimeSpan.FromSeconds(500)).WithRetry(3))
                .WithStep<ShipOrderStep>(x => x.WithTimeout(TimeSpan.FromSeconds(500)).WithRetry(3))
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
