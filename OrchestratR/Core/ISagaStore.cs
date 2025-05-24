
namespace OrchestratR.Core
{
    /// <summary>
    /// Abstraction for saga state storage. Provides methods to save, update, and retrieve saga entities.
    /// </summary>
    public interface ISagaStore
    {
        Task SaveAsync(SagaEntity saga, CancellationToken cancellationToken = default);                              // Save a new saga instance

        Task UpdateAsync(SagaEntity saga, CancellationToken cancellationToken = default);                            // Update existing saga state
        Task UpdateStatusAsync(Guid sagaId, SagaStatus status, CancellationToken cancellationToken = default);       // Update only the status of a saga
        Task UpdateStepIndexAsync(Guid sagaId, int stepIndex, CancellationToken cancellationToken = default);        // Update the current step index of a saga
        Task UpdateContextDataAsync(Guid sagaId, string contextData, CancellationToken cancellationToken = default); // Update the context data of a saga

        Task<SagaEntity?> FindByIdAsync(Guid sagaId, CancellationToken cancellationToken = default);                 // Retrieve saga by its ID
        Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default);  // Retrieve all sagas with a given status
    }
}
