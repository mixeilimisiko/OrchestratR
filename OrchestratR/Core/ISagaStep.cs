
namespace OrchestratR.Core
{
    /// <summary>
    /// Defines a single step in a saga workflow, with execution and compensation logic.
    /// </summary>
    /// <typeparam name="TContext">Type of the SagaContext for this saga.</typeparam>
    public interface ISagaStep<TContext> where TContext : OrchestratR.Core.SagaContext
    {
        /// <summary>
        /// Executes the step logic. Returns a status indicating whether the saga should continue or wait.
        /// Throws exception if an unrecoverable error occurs in this step.
        /// </summary>
        Task<SagaStepStatus> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the compensation (rollback) logic for this step.
        /// This is called if the saga is being rolled back due to a failure in a subsequent step.
        /// It should attempt to undo any changes made in ExecuteAsync.
        /// </summary>
        Task CompensateAsync(TContext context, CancellationToken cancellationToken = default);
    }
}
