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

        public async Task SaveAsync(SagaEntity saga, CancellationToken cancellationToken = default)
        {
            _dbContext.Sagas.Add(saga);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violations etc.
                throw;
            }
        }

        public async Task UpdateAsync(SagaEntity saga, CancellationToken cancellationToken = default)
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
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // handle conflicts
                throw;
            }
        }

        /// <summary>
        /// Partial update: Updates only the status field directly via SQL.
        /// ⚠ Bypasses EF tracking and RowVersion concurrency protection.
        /// </summary>
        public async Task UpdateStatusAsync(Guid sagaId, SagaStatus status, CancellationToken cancellationToken)
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
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, status), cancellationToken);

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

        /// <summary>
        /// Partial update: Updates only the step index.
        /// ⚠ Does not enforce RowVersion matching — use cautiously.
        /// </summary>
        public async Task UpdateStepIndexAsync(Guid sagaId, int stepIndex, CancellationToken cancellationToken = default)
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
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.CurrentStepIndex, stepIndex), cancellationToken);

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

        /// <summary>
        /// Partial update: Updates only the context field.
        /// ⚠ Does not track RowVersion or change detection.
        /// </summary>
        public async Task UpdateContextDataAsync(Guid sagaId, string contextData, CancellationToken cancellationToken = default)
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
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.ContextData, contextData), cancellationToken);

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

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Sagas.FirstOrDefaultAsync(s => s.SagaId == sagaId, cancellationToken);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default)
        {
            return _dbContext.Sagas.AsNoTracking().Where(s => s.Status == status).ToListAsync(cancellationToken);
        }
    }
}