using Microsoft.EntityFrameworkCore;
using OrchestratR.Core;

namespace OrchestratR.Persistence
{
    /// <summary>
    /// Saga store backed by Entity Framework Core for persistent storage in a database.
    /// </summary>
    public class EfCoreSagaStore : ISagaStore
    {
        private readonly SagaDbContext _dbContext;

        public EfCoreSagaStore(SagaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveAsync(SagaEntity saga)
        {
            _dbContext.Sagas.Add(saga);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violations etc.
                throw;
            }
        }

        public async Task UpdateAsync(SagaEntity saga)
        {
            // Check if the entity is already being tracked
            var trackedEntity = _dbContext.Sagas.Local.FirstOrDefault(s => s.SagaId == saga.SagaId);

            if (trackedEntity != null && trackedEntity != saga)
            {
                // If we're tracking a different instance with the same ID, detach it
                _dbContext.Entry(trackedEntity).State = EntityState.Detached;
            }

            // attach and update our entity
            _dbContext.Sagas.Update(saga);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // handle conflicts
                throw;
            }
        }

        public async Task UpdateStatusAsync(Guid sagaId, SagaStatus status)
        {
            // Execute direct SQL update without loading the entity first
            var trackedSaga = _dbContext.Sagas.Local.FirstOrDefault(s => s.SagaId == sagaId);
            if (trackedSaga != null)
            {
                _dbContext.Entry(trackedSaga).State = EntityState.Detached;
            }
            try
            {
                var affected = await _dbContext.Sagas
                    .Where(s => s.SagaId == sagaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, status));

                if (affected == 0)
                {
                    throw new KeyNotFoundException($"Saga with ID {sagaId} not found");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Rethrow concurrency exceptions as they indicate a genuine concurrency conflict
                throw new DbUpdateConcurrencyException($"Concurrency conflict while updating saga {sagaId}", ex);
            }
        }

        public async Task UpdateStepIndexAsync(Guid sagaId, int stepIndex)
        {
            var trackedSaga = _dbContext.Sagas.Local.FirstOrDefault(s => s.SagaId == sagaId);
            if (trackedSaga != null)
            {
                _dbContext.Entry(trackedSaga).State = EntityState.Detached;
            }

            try
            {
                var affected = await _dbContext.Sagas
                    .Where(s => s.SagaId == sagaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.CurrentStepIndex, stepIndex));

                if (affected == 0)
                {
                    throw new KeyNotFoundException($"Saga with ID {sagaId} not found");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new DbUpdateConcurrencyException($"Concurrency conflict while updating saga {sagaId}", ex);
            }
        }

        public async Task UpdateContextDataAsync(Guid sagaId, string contextData)
        {
            var trackedSaga = _dbContext.Sagas.Local.FirstOrDefault(s => s.SagaId == sagaId);
            if (trackedSaga != null)
            {
                _dbContext.Entry(trackedSaga).State = EntityState.Detached;
            }

            try
            {
                var affected = await _dbContext.Sagas
                    .Where(s => s.SagaId == sagaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.ContextData, contextData));

                if (affected == 0)
                {
                    throw new KeyNotFoundException($"Saga with ID {sagaId} not found");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new DbUpdateConcurrencyException($"Concurrency conflict while updating saga {sagaId}", ex);
            }
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId)
        {
            return _dbContext.Sagas.FirstOrDefaultAsync(s => s.SagaId == sagaId);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status)
        {
            return _dbContext.Sagas.AsNoTracking().Where(s => s.Status == status).ToListAsync();
        }
    }
}