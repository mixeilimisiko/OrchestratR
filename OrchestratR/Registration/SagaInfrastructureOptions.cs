using Microsoft.EntityFrameworkCore;
using OrchestratR.Persistence;

namespace OrchestratR.Registration
{

    /// <summary>
    /// Options for configuring saga persistence and infrastructure behaviors.
    /// </summary>
    public class SagaInfrastructureOptions
    {
        internal bool UseEfCoreEnabled { get; private set; }
        internal bool UseInMemoryEnabled { get; private set; }
        internal bool SkipMigrations { get; private set; }
        internal bool TracingEnabled { get; private set; }
        internal Action<DbContextOptionsBuilder>? DbContextOptionsAction { get; private set; }

        /// <summary>
        /// Returns the assembly name that contains EF Core migrations for the saga store.
        /// </summary>
        public static string GetMigrationsAssembly()
            => typeof(SagaDbContext).Assembly.FullName ?? string.Empty;

        /// <summary>
        /// Enables EF Core-backed persistence for sagas, using the provided options action to configure the DbContext.
        /// </summary>
        /// <param name="optionsAction">Callback to configure <see cref="DbContextOptionsBuilder"/> (provider, connection, etc.).</param>
        /// <returns>The same <see cref="SagaInfrastructureOptions"/> instance for chaining.</returns>
        public SagaInfrastructureOptions UseEfCore(Action<DbContextOptionsBuilder> optionsAction)
        {
            UseEfCoreEnabled = true;
            DbContextOptionsAction = dbCtxOptions =>
            {
                optionsAction(dbCtxOptions);
                // MigrationsAssembly logging could be added here if needed
            };
            return this;
        }

        /// <summary>
        /// Enables an in-memory saga store (non-persistent, for testing or simple scenarios).
        /// </summary>
        /// <returns>The same <see cref="SagaInfrastructureOptions"/> instance for chaining.</returns>
        public SagaInfrastructureOptions UseInMemory()
        {
            UseInMemoryEnabled = true;
            return this;
        }

        /// <summary>
        /// Disables automatic application of EF Core migrations on startup.
        /// </summary>
        /// <returns>The same <see cref="SagaInfrastructureOptions"/> instance for chaining.</returns>
        public SagaInfrastructureOptions SkipMigrationApplication()
        {
            SkipMigrations = true;
            return this;
        }

        /// <summary>
        /// Enables tracing instrumentation for saga operations.
        /// </summary>
        /// <returns>The same <see cref="SagaInfrastructureOptions"/> instance for chaining.</returns>
        public SagaInfrastructureOptions UseTracing()
        {
            TracingEnabled = true;
            return this;
        }
    }
}
