using OrchestratR.Core;
using OrchestratR.Orchestration;

namespace OrchestratR.Registration
{
    /// <summary>
    /// Fluent builder for configuring retry and timeout policies on a saga step.
    /// </summary>
    /// <typeparam name="TContext">The saga context type.</typeparam>
    /// <typeparam name="TStep">The saga step implementation type.</typeparam>
    public class StepBuilder<TContext, TStep>
        where TStep : ISagaStep<TContext>
        where TContext : SagaContext
    {
        /// <summary>
        /// Gets the <see cref="System.Type"/> of the saga step being configured.
        /// </summary>
        public Type StepType { get; } = default!;

        private readonly SagaStepDefinition<TContext> _stepDef;

        /// <summary>
        /// Initializes a new instance of the <see cref="StepBuilder{TContext, TStep}"/> class.
        /// </summary>
        /// <param name="stepDef">The underlying saga step definition to apply configurations to.</param>
        internal StepBuilder(SagaStepDefinition<TContext> stepDef)
        {
            _stepDef = stepDef;
        }

        /// <summary>
        /// Specifies the maximum number of retry attempts for this step.
        /// </summary>
        /// <param name="maxRetries">The maximum retry count.</param>
        /// <returns>The current <see cref="StepBuilder{TContext, TStep}"/> instance.</returns>
        public StepBuilder<TContext, TStep> WithRetry(int maxRetries)
        {
            _stepDef.MaxRetries = maxRetries;
            return this;
        }

        /// <summary>
        /// Specifies the timeout duration for this step's execution.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan"/>.</param>
        /// <returns>The current <see cref="StepBuilder{TContext, TStep}"/> instance.</returns>
        public StepBuilder<TContext, TStep> WithTimeout(TimeSpan timeout)
        {
            _stepDef.Timeout = timeout;
            return this;
        }
    }
}
