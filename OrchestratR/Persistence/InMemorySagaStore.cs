
using OrchestratR.Core;
using System.Collections.Concurrent;

namespace OrchestratR.Persistence
{
    /// <summary>
    /// In-memory saga store for testing or lightweight usage. Not durable beyond process lifetime.
    /// </summary>
    public class InMemorySagaStore : ISagaStore
    {
        private readonly ConcurrentDictionary<Guid, SagaEntity> _sagas = new();

        public Task SaveAsync(SagaEntity saga)
        {
            if (!_sagas.TryAdd(saga.SagaId, saga))
            {
                throw new InvalidOperationException($"Saga with ID {saga.SagaId} already exists.");
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SagaEntity saga)
        {
            // Overwrite the existing saga entry (assuming it exists)
            _sagas[saga.SagaId] = saga;
            return Task.CompletedTask;
        }


        public Task UpdateStatusAsync(Guid sagaId, SagaStatus status)
        {
            throw new NotImplementedException();
        }

        public Task UpdateStepIndexAsync(Guid sagaId, int stepIndex)
        {
            throw new NotImplementedException();
        }

        public Task UpdateContextDataAsync(Guid sagaId, string contextData)
        {
            throw new NotImplementedException();
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId)
        {
            _sagas.TryGetValue(sagaId, out SagaEntity? saga);
            return Task.FromResult<SagaEntity?>(saga);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status)
        {
            var result = _sagas.Values.Where(s => s.Status == status).ToList();
            return Task.FromResult(result);
        }
    }
}
