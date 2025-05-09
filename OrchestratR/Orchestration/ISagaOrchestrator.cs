
namespace OrchestratR.Orchestration
{
    /// <summary>
    /// Non-generic interface for orchestrators, used for lookup via DI.
    /// </summary>
    public interface ISagaOrchestrator
    {
        string SagaTypeName { get; }

        Task ResumeAsync(Core.SagaEntity sagaEntity);
    }
}
