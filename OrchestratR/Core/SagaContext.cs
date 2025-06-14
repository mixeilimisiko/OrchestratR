
namespace OrchestratR.Core
{
    /// <summary>
    /// Base class for saga data contexts. 
    /// Users should inherit from this to define data shared across all steps of a saga.
    /// </summary>
    public abstract class SagaContext
    {
        // In this design, SagaId is tracked in SagaEntity rather than in the context.
        // This class mainly exists to provide a base constraint for TContext in SagaOrchestrator and ISagaStep.
    }
}
