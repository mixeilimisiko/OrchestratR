
using OrchestratR.Core;

namespace OrchestratR.Persistence
{
    public class InMemorySagaStore : ISagaStore
    {

        public Task SaveAsync(SagaEntity saga)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SagaEntity saga)
        {
            return Task.CompletedTask;
        }

        public Task<SagaEntity?> FindByIdAsync(Guid sagaId)
        {
            return Task.FromResult<SagaEntity?>(null);
        }

        public Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status)
        {
            return Task.FromResult(new List<SagaEntity>());
        }

    }
}
