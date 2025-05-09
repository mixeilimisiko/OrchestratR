
using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Orchestration;

namespace OrchestratR.Registration
{
    public class SagaBuilder<TContext> where TContext : SagaContext, new()
    {
        private readonly IServiceCollection _services;
        private readonly List<SagaStepDefinition<TContext>> _steps = [];

        internal SagaBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public SagaBuilder<TContext> WithStep<TStep>(Action<StepBuilder<TContext, TStep>>? configure = null) where TStep : class,ISagaStep<TContext>
        {
            // Create a new step definition for this step type
            var stepDef = new SagaStepDefinition<TContext>(typeof(TStep));

            // Register the step implementation type as transient in DI
            _services.AddTransient<TStep>();

            // Apply step-specific configuration if provided
            if (configure != null)
            {
                var stepBuilder = new StepBuilder<TContext, TStep>(stepDef);
                configure(stepBuilder);  // e.g., apply .WithRetry or .WithTimeout
            }
            // Add the step definition to the saga's list (order is preserved)
            _steps.Add(stepDef);
            return this;
        }

        public void Build()
        {
            // Create saga configuration from the collected step definitions
            var sagaConfig = new SagaConfig<TContext>
            {
                Steps = _steps
            };

            // Register the saga config as IOptions<T> so it can be injected
            _services.Configure<SagaConfig<TContext>>(options =>
            {
                options.Steps = sagaConfig.Steps;
            });

            // Register the SagaOrchestrator for this TContext as scoped
            _services.AddScoped<SagaOrchestrator<TContext>>();

            // Also register it as ISagaOrchestrator for non-generic lookup (same instance in scope)
            _services.AddScoped<ISagaOrchestrator>(sp => sp.GetRequiredService<SagaOrchestrator<TContext>>());
        }
    }
}
