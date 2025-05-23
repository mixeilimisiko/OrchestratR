
using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;
using OrchestratR.Orchestration;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrchestratR.Recovery;

namespace OrchestratR.Registration
{
    public class SagaBuilder<TContext> where TContext : SagaContext, new()
    {
        private readonly IServiceCollection _services;
        private readonly List<SagaStepDefinition<TContext>> _steps = [];
        private bool _enableRecovery;

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

                // Compute policy and set it for this stepDefinition
                var policy = new PollyStepPolicyExecutor<TContext>(stepDef.MaxRetries, stepDef.Timeout);
                stepDef.PolicyExecutor = policy;
            }
          
            // Add the step definition to the saga's list (order is preserved)
            _steps.Add(stepDef);
            return this;
        }

        public SagaBuilder<TContext> WithRecovery()
        {
            _enableRecovery = true;
            return this;
        }

        public void Build()
        {
            var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };

            JsonTypeInfo<TContext>? contextTypeInfo;
            try
            {
                contextTypeInfo = (JsonTypeInfo<TContext>?)serializerOptions.GetTypeInfo(typeof(TContext));
            }
            catch
            {
                contextTypeInfo = null;
            }

            // Create saga configuration from the collected step definitions with serializer options
            var sagaConfig = new SagaConfig<TContext>
            {
                Steps = _steps,
                SerializerOptions = serializerOptions,
                ContextTypeInfo = contextTypeInfo
            };

            // Register the saga config as IOptions<T> so it can be injected
            _services.Configure<SagaConfig<TContext>>(options =>
            {
                options.Steps = sagaConfig.Steps;
                options.SerializerOptions = sagaConfig.SerializerOptions;
                options.ContextTypeInfo = sagaConfig.ContextTypeInfo;
            });

            // Register the SagaOrchestrator for this TContext as scoped
            _services.AddScoped<SagaOrchestrator<TContext>>();

            // Also register it as ISagaOrchestrator for non-generic lookup (same instance in scope)
            _services.AddScoped<ISagaOrchestrator>(sp => sp.GetRequiredService<SagaOrchestrator<TContext>>());

            if (_enableRecovery)
            {
                // Register the recovery service *only once* if not already registered
                _services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SagaRecoveryService>());
            }
        }
    }
}
