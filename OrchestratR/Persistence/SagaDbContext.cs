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
            var sagaEntity = modelBuilder.Entity<SagaEntity>();

            sagaEntity.HasKey(e => e.SagaId);
 
            // Configure properties
            sagaEntity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50); // Storing enum as string with max length

            sagaEntity.Property(e => e.SagaType)
                .HasMaxLength(255)
                .IsRequired();

            sagaEntity.Property(e => e.ContextData)
                .HasColumnType("nvarchar(max)");

            sagaEntity.Property(e => e.RowVersion)
                .IsRowVersion();
        }
    }
}
