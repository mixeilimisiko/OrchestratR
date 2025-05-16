using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchestratR.Core;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

// TODO: Refactor for better readability and maintainability
namespace OrchestratR.Orchestration
{
    /// <summary>
    /// Orchestrator responsible for executing saga steps and managing saga state.
    /// </summary>
    /// <typeparam name="TContext">The specific SagaContext type for this saga.</typeparam>
    public class SagaOrchestrator<TContext> : ISagaOrchestrator
        where TContext : SagaContext, new()
    {
        private readonly SagaConfig<TContext> _config;

        private readonly IServiceProvider _provider;
        private readonly ISagaStore _sagaStore;

        private readonly JsonSerializerOptions _serializerOptions;
        private JsonTypeInfo<TContext>? _cachedTypeInfo;

        private const int NotStartedStepIndex = -1;

        public string SagaTypeName { get; } = typeof(TContext).Name;

        public SagaOrchestrator(IOptions<SagaConfig<TContext>> configOptions,
                                IServiceProvider provider,
                                ISagaStore sagaStore)
        {
            _config = configOptions.Value;
            _provider = provider;
            _sagaStore = sagaStore;

            // Initialize serializer options
            _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };

            // Precompute JsonTypeInfo during orchestrator construction
            try
            {
                _cachedTypeInfo = (JsonTypeInfo<TContext>?)_serializerOptions.GetTypeInfo(typeof(TContext));
            }
            catch (Exception)
            {
                _cachedTypeInfo = null;
            }
        }
        /// <summary>Begins execution of a new saga with the given context.</summary>
        public async Task<Guid> StartAsync(TContext context)
        {
            // 1. Create and persist a new SagaEntity for this saga instance.
            var sagaId = Guid.NewGuid();
            var sagaEntity = new SagaEntity
            {
                SagaId = sagaId,
                SagaType = this.SagaTypeName,
                Status = SagaStatus.NotStarted,
                CurrentStepIndex = NotStartedStepIndex,
                ContextData = SerializeContext(context)
            };
            await _sagaStore.SaveAsync(sagaEntity);

            // Immediately mark as InProgress and update (since we are about to execute)
            sagaEntity.Status = SagaStatus.InProgress;
            await _sagaStore.UpdateAsync(sagaEntity);

            // 2. Execute steps in order until done, awaiting, or failure.
            try
            {
                // since exception or transient failure or anything like that
                // is more likely to happen before and during the ExecuteStepWithPolicyAsync
                // we first presist current step index which indicates what step we have not executed yet,
                // though we still have risk of executing same step twice if orchestrator breaks 
                // after step execution but before next iteration starts.
                for (int i = 0; i < _config.Steps.Count; i++)
                {
                    sagaEntity.CurrentStepIndex = i;
                    // Persist the index change (so recovery knows which step we were on)
                    await _sagaStore.UpdateAsync(sagaEntity);

                    // Load context object (it might have been updated in the previous loop iteration)
                    context = DeserializeContext(sagaEntity.ContextData);

                    var stepDef = _config.Steps[i];

                    SagaStepStatus result = await ExecuteStepWithPolicyAsync(stepDef, context);

                    // save the possibly updated context after step execution
                    // we keep ContextData in memory for a small amount of time
                    // it will be persisted either with incremented CurrentStepIndex on next iteration
                    // or with status in case of asynchronous step.

                    // Saving it separately won't avoid the risk of the situation where
                    // step execution is successful but persisting the context fails, it will just
                    // reduce the risk a very little amount which makes it not worth it to do.
                    sagaEntity.ContextData = SerializeContext(context);

                    if (result == SagaStepStatus.Awaiting)
                    {
                        // Step needs external event. Mark saga as Awaiting and stop execution.
                        sagaEntity.Status = SagaStatus.Awaiting;
                        await _sagaStore.UpdateAsync(sagaEntity);
                        return sagaId; // Return early, saga will resume later
                    }

                    // result was Continue, so loop will move to next step
                }

                // All steps completed successfully
                sagaEntity.Status = SagaStatus.Completed;
                sagaEntity.CurrentStepIndex = _config.Steps.Count; // mark index past the last step
                sagaEntity.ContextData = SerializeContext(context);
                await _sagaStore.UpdateAsync(sagaEntity);
            }
            catch (Exception ex)
            {
                // A step threw an error – begin compensation
                sagaEntity.Status = SagaStatus.Compensating;
                await _sagaStore.UpdateAsync(sagaEntity);

                // Determine up to which step had executed (CurrentStepIndex points to the failing step index)
                int failedStepIndex = sagaEntity.CurrentStepIndex;
                // If exception was thrown, the step at failedStepIndex did not complete successfully.
                // We need to compensate all steps before it (i.e., indices 0 to failedStepIndex-1).
                int lastExecutedIndex = failedStepIndex - 1;

                // Compensate in reverse order for all executed steps
                for (int j = lastExecutedIndex; j >= 0; j--)
                {
                    try
                    {
                        // Load latest context (it might have been modified by partial execution or prior compensation)
                        context = DeserializeContext(sagaEntity.ContextData);
                        var stepDef = _config.Steps[j];
                        var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);
                        // TODO: Think if we need to pass the exception to the compensation step
                        // TODO: Think if we need to apply any policies during compensation
                        await step.CompensateAsync(context);
                        // Optionally update context if compensation changes it, then save
                        sagaEntity.ContextData = SerializeContext(context);
                        await _sagaStore.UpdateAsync(sagaEntity);
                    }
                    catch (Exception compEx)
                    {
                        // Compensation for a step failed. Log the error and continue attempting to compensate earlier steps.
                        // (In a real system, you might want to record this in sagaEntity as well.)
                        Console.Error.WriteLine($"Compensation step {j} failed: {compEx}");
                    }
                }

                // After attempting compensation of all executed steps:
                sagaEntity.Status = SagaStatus.Compensated;
                // If any compensation failed (we caught exceptions), one could mark as Failed instead.
                // For simplicity, we'll mark as Compensated since we attempted best effort rollback.
                sagaEntity.CurrentStepIndex = 0;
                await _sagaStore.UpdateAsync(sagaEntity);

                // Rethrow or swallow exception depending on desired behavior.
                // Here, we swallow after compensation, as the saga is considered handled (Compensated).
            }

            return sagaId;
        }

        /// <summary>
        /// Resumes a saga by its ID, continuing from the point it left off.
        /// </summary>
        /// <param name="sagaId">The ID of the saga to resume.</param>
        public async Task ResumeAsync(Guid sagaId)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId)
                              ?? throw new KeyNotFoundException($"Saga with ID {sagaId} not found.");
            await ResumeAsync(sagaEntity);
        }

        /// <summary>
        /// Resumes the saga by applying a context mutation (e.g., x => x.property = something).
        /// </summary>
        /// <param name="sagaId">The ID of the saga to resume.</param>
        /// <param name="patch">
        /// A lambda that modifies the existing context object in place. 
        /// Do not assign a new object to <c>ctx</c>, only change its properties.
        /// </param>
        /// <exception cref="KeyNotFoundException">Thrown if saga is not found.</exception>
        public async Task ResumeAsync(Guid sagaId, Action<TContext> patch)
        {
            // Fetch saga
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId) 
                ?? throw new KeyNotFoundException($"Saga with ID {sagaId} not found.");

            // Deserialize context
            var context = DeserializeContext(sagaEntity.ContextData);
            var originalRef = context;
            // Apply user-provided mutation to the context
            patch(context);

            // Defensive check: ensure they didn’t reassign context variable
            if (!ReferenceEquals(context, originalRef))
                throw new InvalidOperationException("Patch must not assign a new instance to the context. Modify the existing object.");

            // Serialize the updated context back to JSON
            sagaEntity.ContextData = SerializeContext(context);

            // Save modified context (we don’t change status/step yet)
            await _sagaStore.UpdateAsync(sagaEntity);

            // Resume normal orchestrator flow
            await ResumeAsync(sagaEntity);
        }

        /// <summary>
        /// Resumes an incomplete saga (e.g., Awaiting or interrupted) from a stored SagaEntity.
        /// </summary>
        public async Task ResumeAsync(SagaEntity sagaEntity)
        {
            // Reconstruct context from stored data
            var context = DeserializeContext(sagaEntity.ContextData);

            if (sagaEntity.Status == SagaStatus.Awaiting)
            {
                // Saga was waiting for an external trigger, presumably the condition is now met.
                // Continue from the current step (which had returned Awaiting).
                sagaEntity.Status = SagaStatus.InProgress;
                // Increase current step index
                sagaEntity.CurrentStepIndex++;
                await _sagaStore.UpdateAsync(sagaEntity);
            }
            //else if (sagaEntity.Status == SagaStatus.InProgress)
            //{
            //    // Saga was in the middle of execution when interrupted (crash scenario).
            //    // We will resume from the CurrentStepIndex.
            //    // Possibly re-run the current step if it didn't finish, assuming idempotency.
            //}
            //else if (sagaEntity.Status == SagaStatus.Compensating)
            //{
            //    // Saga was in the middle of compensation when interrupted.
            //    // We will continue compensating remaining steps.
            //}

            // Resume forward execution if applicable
            if (sagaEntity.Status == SagaStatus.InProgress)
            {
                try
                {
                    // Start from the current step index
                    for (int i = sagaEntity.CurrentStepIndex; i < _config.Steps.Count; i++)
                    {
                        sagaEntity.CurrentStepIndex = i;
                        await _sagaStore.UpdateAsync(sagaEntity);

                        // Ensure we have latest context (could have been modified externally or earlier)
                        context = DeserializeContext(sagaEntity.ContextData);
                        var stepDef = _config.Steps[i];
                        //var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);

                        SagaStepStatus result = await ExecuteStepWithPolicyAsync(stepDef, context);
                        sagaEntity.ContextData = SerializeContext(context);

                        if (result == SagaStepStatus.Awaiting)
                        {
                            sagaEntity.Status = SagaStatus.Awaiting;
                            await _sagaStore.UpdateAsync(sagaEntity);
                            return; // pause again, awaiting another external event
                        }
                        // else continue loop
                    }

                    // If we exit loop normally, saga now completed
                    sagaEntity.Status = SagaStatus.Completed;
                    sagaEntity.CurrentStepIndex = _config.Steps.Count;
                    sagaEntity.ContextData = SerializeContext(context);
                    await _sagaStore.UpdateAsync(sagaEntity);
                }
                catch (Exception ex)
                {
                    // If an exception occurs on resume, handle similar to Start logic
                    sagaEntity.Status = SagaStatus.Compensating;
                    await _sagaStore.UpdateAsync(sagaEntity);
                    int failedStepIndex = sagaEntity.CurrentStepIndex;
                    int lastExecutedIndex = failedStepIndex - 1;
                    for (int j = lastExecutedIndex; j >= 0; j--)
                    {
                        try
                        {
                            context = DeserializeContext(sagaEntity.ContextData);
                            var stepDef = _config.Steps[j];
                            var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);
                            await step.CompensateAsync(context);
                            sagaEntity.ContextData = SerializeContext(context);
                            await _sagaStore.UpdateAsync(sagaEntity);
                        }
                        catch (Exception compEx)
                        {
                            Console.Error.WriteLine($"Compensation step {j} failed during resume: {compEx}");
                        }
                    }
                    sagaEntity.Status = SagaStatus.Compensated;
                    await _sagaStore.UpdateAsync(sagaEntity);
                }
            }

            // Resume compensation if saga was mid-compensation
            if (sagaEntity.Status == SagaStatus.Compensating)
            {
                // Find which step was last compensated (if any).
                // For simplicity, assume compensation starts from last executed step if not already done.
                int lastIndexToCompensate = sagaEntity.CurrentStepIndex - 1;
                if (lastIndexToCompensate < 0) return; //lastIndexToCompensate = _config.Steps.Count - 1;
                for (int j = lastIndexToCompensate; j >= 0; j--)
                {
                    try
                    {
                        context = DeserializeContext(sagaEntity.ContextData);
                        var stepDef = _config.Steps[j];
                        var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);
                        await step.CompensateAsync(context);
                        sagaEntity.ContextData = SerializeContext(context);
                        await _sagaStore.UpdateAsync(sagaEntity);
                    }
                    catch (Exception compEx)
                    {
                        Console.Error.WriteLine($"Compensation step {j} failed (resume): {compEx}");
                    }
                }
                sagaEntity.Status = SagaStatus.Compensated;
                sagaEntity.CurrentStepIndex = 0;
                await _sagaStore.UpdateAsync(sagaEntity);
            }
        }

        #region StepExecution
        // Helper: Execute a step with its associated policy (if any)
        private Task<SagaStepStatus> ExecuteStepWithPolicyAsync(SagaStepDefinition<TContext> stepDef, TContext context)
        {
            var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);

            return stepDef.PolicyExecutor is not null
                ? stepDef.PolicyExecutor.ExecuteAsync(() => step.ExecuteAsync(context))
                : step.ExecuteAsync(context);
        }
        #endregion StepExecution

        #region Serialization/Deserialization

        // Helper: Serialize context to JSON string
        private string SerializeContext(TContext context)
        {
            try
            {
                _cachedTypeInfo ??= (JsonTypeInfo<TContext>?)_serializerOptions.GetTypeInfo(typeof(TContext));
                return JsonSerializer.Serialize(context, _cachedTypeInfo!);
            }
            catch (Exception)
            {
                return JsonSerializer.Serialize(context, _serializerOptions); // fallback serialization
            }
        }

        // Helper: Deserialize context from JSON string
        private TContext DeserializeContext(string jsonData)
        {
            try
            {
                _cachedTypeInfo ??= (JsonTypeInfo<TContext>?)_serializerOptions.GetTypeInfo(typeof(TContext));
                return JsonSerializer.Deserialize(jsonData, _cachedTypeInfo!) ?? new TContext();
            }
            catch (Exception)
            {
                return JsonSerializer.Deserialize<TContext>(jsonData, _serializerOptions) ?? new TContext(); // fallback deserialization
            }
        }

        #endregion Serialization/Deserialization
    }
}