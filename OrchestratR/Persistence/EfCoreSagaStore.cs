using Microsoft.EntityFrameworkCore;
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

        public async Task SaveAsync(SagaEntity saga)
        {
            _db.Sagas.Add(saga);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violations etc.
                throw;
            }
        }

        public async Task UpdateAsync(SagaEntity saga)
        {
            // We assume saga was tracked or attach and update
            _db.Sagas.Update(saga);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // If using concurrency tokens, handle conflicts
                throw;
            }
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId)
        {
            return _db.Sagas.FirstOrDefaultAsync(s => s.SagaId == sagaId);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status)
        {
            return _db.Sagas.Where(s => s.Status == status).ToListAsync();
        }
    }
}
