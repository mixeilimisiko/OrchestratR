using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Persistence;
using OrchestratR.Tracing;

namespace OrchestratR.Registration
{
    public static class SagaRegistrationExtensions
    {
        public static SagaBuilder<TContext> AddSaga<TContext>(this IServiceCollection services)
            where TContext : SagaContext, new()
        {

            return new SagaBuilder<TContext>(services);
        }

        public static IServiceCollection AddSagaInfrastructure(this IServiceCollection services, Action<SagaInfrastructureOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new SagaInfrastructureOptions();
            configure(options);

            if (options.UseEfCoreEnabled)
            {
                // Using DbContext pooling for efficiency (optional; can use AddDbContext as well)
                services.AddDbContextPool<SagaDbContext>(options.DbContextOptionsAction!);

                // Register the EF Core implementation of ISagaStore
                services.AddScoped<ISagaStore, EfCoreSagaStore>();

                // If auto-migration is NOT skipped, register the migration hosted service
                if (!options.SkipMigrations)
                {
                    services.AddHostedService<SagaMigrationService>();
                }
            }

            if (options.UseInMemoryEnabled)
            {
                services.AddSingleton<ISagaStore, InMemorySagaStore>();
            }

            if (options.TracingEnabled)
            {
                services.AddSingleton<ISagaTelemetry, SagaTelemetry>();
            }
            else
            {
                services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();
            }
            
            return services;
        }
    }
}
