
namespace OrchestratR.Core
{
    /// <summary>
    /// Indicates the outcome of a saga step execution.
    /// </summary>
    public enum SagaStepStatus
    {
        /// <summary>
        ///Step completed successfully; proceed to the next step immediately.
        /// </summary>
        Continue,
        /// <summary>
        /// Step initiated an async process; saga should pause and wait for an external trigger to resume.
        /// </summary>
        Awaiting
        // (No explicit "Failed" status here – failures are indicated by throwing exceptions from ExecuteAsync.)
    }
}
