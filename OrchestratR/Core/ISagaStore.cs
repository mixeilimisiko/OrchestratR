
namespace OrchestratR.Core
{
    /// <summary>
    /// Abstraction for saga state storage. Provides methods to save, update, and retrieve saga entities.
    /// </summary>
    public interface ISagaStore
    {
        /// <summary>
        /// Persists a new saga instance.
        /// </summary>
        /// <param name="saga">The saga entity to save.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        Task SaveAsync(SagaEntity saga, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the full state of an existing saga.
        /// </summary>
        /// <param name="saga">The saga entity with updated state.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        Task UpdateAsync(SagaEntity saga, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates only the status of an existing saga.
        /// </summary>
        /// <param name="sagaId">The unique identifier of the saga.</param>
        /// <param name="status">The new status to apply.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        Task UpdateStatusAsync(Guid sagaId, SagaStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the current step index of an existing saga.
        /// </summary>
        /// <param name="sagaId">The unique identifier of the saga.</param>
        /// <param name="stepIndex">The index of the next step to execute.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        Task UpdateStepIndexAsync(Guid sagaId, int stepIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the serialized context data of an existing saga.
        /// </summary>
        /// <param name="sagaId">The unique identifier of the saga.</param>
        /// <param name="contextData">JSON-serialized context object.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        Task UpdateContextDataAsync(Guid sagaId, string contextData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a saga entity by its identifier.
        /// </summary>
        /// <param name="sagaId">The unique identifier of the saga.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        /// <returns>The saga entity if found; otherwise, <c>null</c>.</returns>
        Task<SagaEntity?> FindByIdAsync(Guid sagaId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all saga entities with the specified status.
        /// </summary>
        /// <param name="status">The status to filter by.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
        /// <returns>A list of saga entities matching the given status.</returns>
        Task<List<SagaEntity>> FindByStatusAsync(SagaStatus status, CancellationToken cancellationToken = default);
    }
}
