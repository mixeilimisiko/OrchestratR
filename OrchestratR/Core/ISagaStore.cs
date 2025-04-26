
namespace OrchestratR.Core
{
    /// <summary>
    /// Abstraction for saga state storage. Provides methods to save, update, and retrieve saga entities.
    /// </summary>
    public interface ISagaStore
    {
        Task SaveAsync(SagaEntity saga);                              // Save a new saga instance

        Task UpdateAsync(SagaEntity saga);                            // Update existing saga state
        Task UpdateStatusAsync(Guid sagaId, SagaStatus status);       // Update only the status of a saga
        Task UpdateStepIndexAsync(Guid sagaId, int stepIndex);        // Update the current step index of a saga
        Task UpdateContextDataAsync(Guid sagaId, string contextData); // Update the context data of a saga

        Task<SagaEntity?> FindByIdAsync(Guid sagaId);                 // Retrieve saga by its ID
        Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status);  // Retrieve all sagas with a given status
    }
}
