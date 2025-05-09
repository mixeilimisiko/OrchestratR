
using OrchestratR.Core;
using OrchestratR.Orchestration;

namespace OrchestratR.Builder
{
    public class StepBuilder<TContext, TStep> where TStep : ISagaStep<TContext> where TContext : SagaContext
    {
        public Type StepType { get; } = default!;
        private readonly SagaStepDefinition<TContext> _stepDef;
        internal StepBuilder(SagaStepDefinition<TContext> stepDef) { _stepDef = stepDef; }

        public StepBuilder<TContext, TStep> WithRetry(int maxRetries)
        {
            _stepDef.MaxRetries = maxRetries;
            return this;
        }
        public StepBuilder<TContext, TStep> WithTimeout(TimeSpan timeout)
        {
            _stepDef.Timeout = timeout;
            return this;
        }
    }
}
