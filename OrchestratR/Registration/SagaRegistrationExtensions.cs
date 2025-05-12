using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Persistence;

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

            return services;
        }
    }


    public class SagaInfrastructureOptions
    {
        internal bool UseEfCoreEnabled { get; private set; }
        internal bool UseInMemoryEnabled { get; private set; }
        internal bool SkipMigrations { get; private set; }
        internal Action<DbContextOptionsBuilder>? DbContextOptionsAction { get; private set; }

        public static string GetMigrationsAssembly()
        {
            return typeof(SagaDbContext).Assembly.FullName ?? string.Empty;
        }

        // Consumer will call this to use EF Core as the saga store
        public SagaInfrastructureOptions UseEfCore(Action<DbContextOptionsBuilder> optionsAction)
        {
            UseEfCoreEnabled = true;

            DbContextOptionsAction = dbCtxOptions =>
            {
                // Let the user fully configure provider and connection
                optionsAction(dbCtxOptions);

                // log if MigrationsAssembly is missing
                // users should configure it themselves. 

            };

            return this;
        }
        public SagaInfrastructureOptions UseInMemory()
        {
            UseInMemoryEnabled = true;

            return this;
        }

        // Consumer can call this to disable auto-migration
        public SagaInfrastructureOptions SkipMigrationApplication()
        {
            SkipMigrations = true;
            return this;
        }
    }
}
