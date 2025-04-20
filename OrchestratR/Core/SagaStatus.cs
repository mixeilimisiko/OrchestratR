
namespace OrchestratR.Core
{
    /// <summary>
    /// Represents the current execution status of a saga.
    /// </summary>
    public enum SagaStatus
    {
        NotStarted,    // Saga created but not yet executed
        InProgress,    // Saga is actively running steps
        Awaiting,      // Saga is waiting for an external event before continuing
        Completed,     // Saga finished all steps successfully
        Compensating,  // Saga is undoing executed steps due to a failure
        Compensated,   // Saga completed compensation (rolled back after failure)
        Failed         // Saga failed without (or before completing) compensation
    }
}
