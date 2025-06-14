using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Persistence;
using OrchestratR.Tracing;

namespace OrchestratR.Registration
{
    /// <summary>
    /// Extension methods for registering saga orchestrators and persistence infrastructure.
    /// </summary>
    public static class SagaRegistrationExtensions
    {
        /// <summary>
        /// Begins registration of a saga for the specified context type.
        /// </summary>
        /// <typeparam name="TContext">The concrete <see cref="SagaContext"/> type.</typeparam>
        /// <param name="services">The service collection to add saga registrations to.</param>
        /// <returns>A <see cref="SagaBuilder{TContext}"/> for fluently defining saga steps and recovery.</returns>
        public static SagaBuilder<TContext> AddSaga<TContext>(this IServiceCollection services)
            where TContext : SagaContext, new()
        {
            return new SagaBuilder<TContext>(services);
        }

        /// <summary>
        /// Configures and registers the saga persistence and telemetry infrastructure.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configure">
        /// A callback to configure <see cref="SagaInfrastructureOptions"/>, 
        /// choosing EF Core or in-memory store, migrations, and tracing.
        /// </param>
        /// <returns>The original <paramref name="services"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.
        /// </exception>
        public static IServiceCollection AddSagaInfrastructure(
            this IServiceCollection services,
            Action<SagaInfrastructureOptions> configure)
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
                // Register an in-memory saga store for non-persistent scenarios
                services.AddSingleton<ISagaStore, InMemorySagaStore>();
            }

            if (options.TracingEnabled)
            {
                // Register tracing-enabled telemetry
                services.AddSingleton<ISagaTelemetry, SagaTelemetry>();
            }
            else
            {
                // Register a no-op telemetry implementation
                services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();
            }

            return services;
        }
    }
}
