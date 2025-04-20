using Microsoft.EntityFrameworkCore;
using OrchestratR.Core;


namespace OrchestratR.Persistence
{
    public interface ISagaDbContext
    {
        public DbSet<SagaEntity> Sagas { get; set; }
    }
}
