using OrchestratR.Core;

namespace OrchestratR.Persistence
{

    /// <summary>
    /// Saga store backed by Entity Framework Core for persistent storage in a database.
    /// </summary>
    public class EfCoreSagaStore : ISagaStore
    {
        private readonly SagaDbContext _db;

        public EfCoreSagaStore(SagaDbContext dbContext)
        {
            _db = dbContext;
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId)
        {
            throw new NotImplementedException();
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync(SagaEntity saga)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(SagaEntity saga)
        {
            throw new NotImplementedException();
        }
    }
}
