
namespace OrchestratR.Core
{
    /// <summary>
    /// Indicates the outcome of a saga step execution.
    /// </summary>
    public enum SagaStepStatus
    {
        Continue,  // Step completed successfully; proceed to the next step immediately.
        Awaiting   // Step initiated an async process; saga should pause and wait for an external trigger to resume.
        // (No explicit "Failed" status here – failures are indicated by throwing exceptions from ExecuteAsync.)
    }
}
