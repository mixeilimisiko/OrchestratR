using OrchestratR.Core;

namespace OrchestratR.Orchestration
{
    public class SagaConfig<TContext> where TContext : SagaContext
    {
        public List<SagaStepDefinition<TContext>> Steps { get; set; } = [];
    }
}
