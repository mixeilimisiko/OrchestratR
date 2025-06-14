
namespace OrchestratR.Core
{

    /// <summary>
    /// Represents the current execution status of a saga.
    /// </summary>
    public enum SagaStatus
    {
        /// <summary>
        /// Saga entity created but no steps have been executed yet.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Saga is actively running through its steps.
        /// </summary>
        InProgress,

        /// <summary>
        /// Saga has paused awaiting an external event or callback.
        /// </summary>
        Awaiting,

        /// <summary>
        /// All steps completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// An error occurred; saga is running compensation (undo) steps.
        /// </summary>
        Compensating,

        /// <summary>
        /// All compensation steps completed; saga has been rolled back.
        /// </summary>
        Compensated,

        /// <summary>
        /// Saga failed without (or before) completing compensation.
        /// </summary>
        Failed
    }
}
