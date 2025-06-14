
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
     /// <summary>
    /// Fluent builder for registering a saga and its steps into the DI container.
    /// </summary>
    /// <typeparam name="TContext">The concrete <see cref="SagaContext"/> type for this saga.</typeparam>
    public class SagaBuilder<TContext> where TContext : SagaContext, new()
    {
        private readonly IServiceCollection _services;
        private readonly List<SagaStepDefinition<TContext>> _steps = new();
        private bool _enableRecovery;

        /// <summary>
        /// Creates a new <see cref="SagaBuilder{TContext}"/> backed by the given service collection.
        /// </summary>
        /// <param name="services">The DI service collection to register saga components into.</param>
        internal SagaBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Adds a step of type <typeparamref name="TStep"/> to the saga, with optional retry/timeout configuration.
        /// </summary>
        /// <typeparam name="TStep">The implementation of <see cref="ISagaStep{TContext}"/> to execute.</typeparam>
        /// <param name="configure">
        /// Optional callback to configure policies on the step via a <see cref="StepBuilder{TContext, TStep}"/>.
        /// </param>
        /// <returns>The same <see cref="SagaBuilder{TContext}"/> instance for chaining.</returns>
        public SagaBuilder<TContext> WithStep<TStep>(Action<StepBuilder<TContext, TStep>>? configure = null)
            where TStep : class, ISagaStep<TContext>
        {
            var stepDef = new SagaStepDefinition<TContext>(typeof(TStep));
            _services.AddTransient<TStep>();

            if (configure != null)
            {
                var stepBuilder = new StepBuilder<TContext, TStep>(stepDef);
                configure(stepBuilder);
                stepDef.PolicyExecutor = new PollyStepPolicyExecutor<TContext>(stepDef.MaxRetries, stepDef.Timeout);
            }

            _steps.Add(stepDef);
            return this;
        }

        /// <summary>
        /// Enables background recovery for this saga via the <see cref="SagaRecoveryService"/>.
        /// </summary>
        /// <returns>The same <see cref="SagaBuilder{TContext}"/> instance for chaining.</returns>
        public SagaBuilder<TContext> WithRecovery()
        {
            _enableRecovery = true;
            return this;
        }

        /// <summary>
        /// Finalizes registration: wires up <see cref="SagaConfig{TContext}"/>, <see cref="SagaOrchestrator{TContext}"/>,
        /// </summary>
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

            var sagaConfig = new SagaConfig<TContext>
            {
                Steps = _steps,
                SerializerOptions = serializerOptions,
                ContextTypeInfo = contextTypeInfo
            };

            _services.Configure<SagaConfig<TContext>>(options =>
            {
                options.Steps = sagaConfig.Steps;
                options.SerializerOptions = sagaConfig.SerializerOptions;
                options.ContextTypeInfo = sagaConfig.ContextTypeInfo;
            });

            _services.AddScoped<SagaOrchestrator<TContext>>();
            _services.AddScoped<ISagaOrchestrator>(sp => sp.GetRequiredService<SagaOrchestrator<TContext>>());

            if (_enableRecovery)
            {
                _services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SagaRecoveryService>());
            }
        }
    }
}
