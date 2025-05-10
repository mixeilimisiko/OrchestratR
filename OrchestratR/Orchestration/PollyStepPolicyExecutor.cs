using OrchestratR.Core;
using Polly;
using Polly.Timeout;

namespace OrchestratR.Orchestration
{
    public class PollyStepPolicyExecutor<TContext> : IStepPolicyExecutor<TContext>
    {
        private readonly IAsyncPolicy<SagaStepStatus> _policy;

        public PollyStepPolicyExecutor(int maxRetries, TimeSpan? timeout)
        {
            IAsyncPolicy<SagaStepStatus> retryPolicy = Policy<SagaStepStatus>
                .Handle<Exception>()
                .RetryAsync(maxRetries);

            if (timeout.HasValue)
            {
                // Timeout policy — applies per execution attempt
                IAsyncPolicy<SagaStepStatus> timeoutPolicy = Policy
                    .TimeoutAsync<SagaStepStatus>(timeout.Value, TimeoutStrategy.Optimistic);

                // Compose timeout first, then retry
                _policy = Policy.WrapAsync(retryPolicy, timeoutPolicy);
            }
            else
            {
                _policy = retryPolicy;
            }
        }

        public Task<SagaStepStatus> ExecuteAsync(Func<Task<SagaStepStatus>> stepExecution)
            => _policy.ExecuteAsync(stepExecution);
    }
}
