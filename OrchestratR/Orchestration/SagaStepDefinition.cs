using OrchestratR.Core;

namespace OrchestratR.Orchestration
{
    public class SagaStepDefinition<TContext> where TContext : SagaContext
    {
        public Type StepType { get; }
        public int MaxRetries { get; set; }
        public TimeSpan? Timeout { get; set; }

        public SagaStepDefinition(Type stepType)
        {
            StepType = stepType;
        }
    }
}
