using Microsoft.EntityFrameworkCore;
using OrchestratR.Persistence;

namespace OrchestratR.Registration
{

    public class SagaInfrastructureOptions
    {
        internal bool UseEfCoreEnabled { get; private set; }
        internal bool UseInMemoryEnabled { get; private set; }
        internal bool SkipMigrations { get; private set; }
        internal bool TracingEnabled { get; private set; }

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


        public SagaInfrastructureOptions UseTracing()
        {
            TracingEnabled = true;
            return this;
        }
    }
}
