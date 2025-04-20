using Microsoft.EntityFrameworkCore;
using OrchestratR.Core;

namespace OrchestratR.Persistence
{
    public class SagaDbContext : DbContext
    {
        public DbSet<SagaEntity> Sagas { get; set; } = null!;
        public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SagaEntity>()
                .HasKey(e => e.SagaId);
            // configure the ContextData to be stored as JSON in a column if using a database that supports JSON.
            // for simplicity, take a text column.
            modelBuilder.Entity<SagaEntity>()
                .Property(e => e.Status).HasConversion<string>();
            // Storing the enum as string for readability, might change to int for performance later
        }
    }
}
