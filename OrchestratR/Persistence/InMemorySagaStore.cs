
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

        public Task SaveAsync(SagaEntity saga, CancellationToken cancellationToken = default)
        {
            if (!_sagas.TryAdd(saga.SagaId, saga))
            {
                throw new InvalidOperationException($"Saga with ID {saga.SagaId} already exists.");
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SagaEntity saga, CancellationToken cancellationToken = default)
        {
            // Overwrite the existing saga entry (assuming it exists)
            _sagas[saga.SagaId] = saga;
            return Task.CompletedTask;
        }


        public Task UpdateStatusAsync(Guid sagaId, SagaStatus status, CancellationToken cancellationToken = default)
        {
            if (_sagas.TryGetValue(sagaId, out var saga))
            {
                saga.Status = status;
            }
            else
            {
                throw new InvalidOperationException($"Saga with ID {sagaId} not found.");
            }
            return Task.CompletedTask;
        }

        public Task UpdateStepIndexAsync(Guid sagaId, int stepIndex, CancellationToken cancellationToken = default)
        {
            if (_sagas.TryGetValue(sagaId, out var saga))
            {
                saga.CurrentStepIndex = stepIndex;
            }
            else
            {
                throw new InvalidOperationException($"Saga with ID {sagaId} not found.");
            }
            return Task.CompletedTask;
        }

        public Task UpdateContextDataAsync(Guid sagaId, string contextData, CancellationToken cancellationToken = default)
        {
            if (_sagas.TryGetValue(sagaId, out var saga))
            {
                saga.ContextData = contextData;
            }
            else
            {
                throw new InvalidOperationException($"Saga with ID {sagaId} not found.");
            }
            return Task.CompletedTask;
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId, CancellationToken cancellationToken = default)
        {
            _sagas.TryGetValue(sagaId, out SagaEntity? saga);
            return Task.FromResult<SagaEntity?>(saga);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default)
        {
            var result = _sagas.Values.Where(s => s.Status == status).ToList();
            return Task.FromResult(result);
        }
    }
}
