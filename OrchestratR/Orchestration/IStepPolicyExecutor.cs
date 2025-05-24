using OrchestratR.Core;

namespace OrchestratR.Orchestration
{
    public interface IStepPolicyExecutor<TContext>
    {
        Task<SagaStepStatus> ExecuteAsync(Func<CancellationToken, Task<SagaStepStatus>> stepExecution, CancellationToken cancellationToken);
    }
}
