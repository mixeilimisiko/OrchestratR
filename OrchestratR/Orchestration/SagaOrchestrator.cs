using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchestratR.Core;
using OrchestratR.Tracing;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
        private readonly ISagaTelemetry _telemetry;

        private const int NotStartedStepIndex = -1;

        public string SagaTypeName { get; } = typeof(TContext).Name;

        public SagaOrchestrator(IOptions<SagaConfig<TContext>> configOptions,
                                IServiceProvider provider,
                                ISagaStore sagaStore,
                                ISagaTelemetry telemetry)
        {
            _config = configOptions.Value;
            _provider = provider;
            _sagaStore = sagaStore;
            _telemetry = telemetry;
        }

        /// <summary>Begins execution of a new saga with the given context.</summary>
        public async Task<Guid> StartAsync(TContext context, CancellationToken cancellationToken = default)
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
            await _sagaStore.SaveAsync(sagaEntity, cancellationToken);

            // Log saga creation
            _telemetry.LogSagaEvent(SagaEventType.Started, sagaId, SagaTypeName, SagaStatus.NotStarted,
                SagaLogLevel.Information, "Saga created and ready to start");

            using var activity = _telemetry.StartSaga(sagaId, SagaTypeName, "Start");

            // Immediately mark as InProgress and update (since we are about to execute)
            sagaEntity.Status = SagaStatus.InProgress;
            sagaEntity.CurrentStepIndex = 0;
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            // Log status change to InProgress
            _telemetry.LogSagaEvent(SagaEventType.StatusChanged, sagaId, SagaTypeName, SagaStatus.InProgress,
                SagaLogLevel.Information, "Saga status changed to InProgress");

            // 2. Execute steps in order until done, awaiting, or failure.
            return await ExecuteSagaFlowAsync(sagaEntity, activity, cancellationToken);
        }

        /// <summary>
        /// Resumes a saga by its ID, continuing from the point it left off.
        /// </summary>
        /// <param name="sagaId">The ID of the saga to resume.</param>
        /// <param name="cancellationToken">Cancellation Token </param>
        public async Task ResumeAsync(Guid sagaId, CancellationToken cancellationToken = default)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId, cancellationToken)
                              ?? throw new KeyNotFoundException($"Saga with ID {sagaId} not found.");

            ValidateSagaStatusForResume(sagaEntity.Status);

            // Log saga resume
            _telemetry.LogSagaEvent(SagaEventType.Resumed, sagaId, sagaEntity.SagaType, sagaEntity.Status,
                SagaLogLevel.Information, "Saga resume requested");

            await ResumeAsync(sagaEntity, cancellationToken);
        }

        /// <summary>
        /// Resumes the saga by applying a context mutation (e.g., x => x.property = something).
        /// </summary>
        /// <param name="sagaId">The ID of the saga to resume.</param>
        /// <param name="patch">
        /// A lambda that modifies the existing context object in place. 
        /// Do not assign a new object to <c>ctx</c>, only change its properties.
        /// </param>
        /// <param name="cancellationToken">Cancellation Token </param>
        /// <exception cref="KeyNotFoundException">Thrown if saga is not found.</exception>
        public async Task ResumeAsync(Guid sagaId, Action<TContext> patch, CancellationToken cancellationToken = default)
        {
            // Fetch saga
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId, cancellationToken)
                ?? throw new KeyNotFoundException($"Saga with ID {sagaId} not found.");

            ValidateSagaStatusForResume(sagaEntity.Status);

            // Log context update
            _telemetry.LogSagaEvent(SagaEventType.ContextUpdated, sagaId, sagaEntity.SagaType, sagaEntity.Status,
                SagaLogLevel.Information, "Saga context will be updated before resume");

            // Deserialize context
            var context = DeserializeContext(sagaEntity.ContextData);
            // Apply user-provided mutation to the context
            patch(context);

            // Serialize the updated context back to JSON
            sagaEntity.ContextData = SerializeContext(context);

            // Save modified context (we don't change status/step yet)
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            // Log context updated
            _telemetry.LogSagaEvent(SagaEventType.ContextUpdated, sagaId, sagaEntity.SagaType, sagaEntity.Status,
                SagaLogLevel.Information, "Saga context updated successfully");

            // Resume normal orchestrator flow
            await ResumeAsync(sagaEntity, cancellationToken);
        }

        /// <summary>
        /// Resumes an incomplete saga (e.g., Awaiting or interrupted) from a stored SagaEntity.
        /// </summary>
        public async Task ResumeAsync(SagaEntity sagaEntity, CancellationToken cancellationToken = default)
        {
            // Reconstruct context from stored data
            var context = DeserializeContext(sagaEntity.ContextData);

            using var activity = _telemetry.StartSaga(sagaEntity.SagaId, sagaEntity.SagaType, "Resume");

            var resumeHandler = GetResumeHandler(sagaEntity.Status);

            await resumeHandler(sagaEntity, activity, cancellationToken);
        }

        #region Core Execution Logic

        /// <summary>
        /// Central execution flow that handles both start and resume scenarios.
        /// Executes forward steps, handles completion, and manages exceptions with compensation.
        /// </summary>
        private async Task<Guid> ExecuteSagaFlowAsync(SagaEntity sagaEntity, Activity? activity, CancellationToken cancellationToken)
        {
            try
            {
                await ExecuteForwardStepsAsync(sagaEntity, cancellationToken);
                if (sagaEntity.Status == SagaStatus.Completed)
                {
                    _telemetry.MarkCompleted(activity);
                }
            }
            catch (OperationCanceledException ex)
            {
                // Cancellation was triggered (e.g., HTTP request aborted or host shutting down)
                // Gracefully stop without compensation — leave saga InProgress
                _telemetry.LogSagaEvent(SagaEventType.Cancelled, sagaEntity.SagaId, sagaEntity.SagaType, sagaEntity.Status,
                    SagaLogLevel.Warning, "Saga execution was cancelled gracefully");
                _telemetry.RecordException(activity, ex);
                return sagaEntity.SagaId;
            }
            catch (Exception ex)
            {
                _telemetry.RecordException(activity, ex);
                _telemetry.LogSagaEvent(SagaEventType.Failed, sagaEntity.SagaId, sagaEntity.SagaType, sagaEntity.Status, ex,
                    SagaLogLevel.Error);
                await HandleSagaFailureAsync(sagaEntity, ex, cancellationToken);
            }

            return sagaEntity.SagaId;
        }

        /// <summary>
        /// Executes steps sequentially from the current step index until completion or awaiting state.
        /// </summary>
        private async Task ExecuteForwardStepsAsync(SagaEntity sagaEntity, CancellationToken cancellationToken)
        {
            // since exception or transient failure or anything like that
            // is more likely to happen before and during the ExecuteStepWithPolicyAsync
            // we first presist current step index which indicates what step we have not executed yet,
            // though we still have risk of executing same step twice if orchestrator breaks 
            // after step execution but before next iteration starts.
            for (int i = sagaEntity.CurrentStepIndex; i < _config.Steps.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Stop gracefully, keep state intact
                    await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);
                    return;
                }

                sagaEntity.CurrentStepIndex = i;
                // Persist the index change (so recovery knows which step we were on)
                await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

                // Load context object (it might have been updated in the previous loop iteration)
                var context = DeserializeContext(sagaEntity.ContextData);
                var stepDef = _config.Steps[i];

                // Start step telemetry
                using var stepActivity = _telemetry.StartStep(sagaEntity.SagaId, stepDef.StepType.Name, i);

                _telemetry.LogStepEvent(StepEventType.Started, sagaEntity.SagaId, sagaEntity.SagaType,
                    stepDef.StepType.Name, i, SagaStepStatus.Continue, SagaLogLevel.Information,
                    $"Starting execution of step: {stepDef.StepType}");

                SagaStepStatus result;
                try
                {
                    result = await ExecuteStepWithPolicyAsync(stepDef, context, cancellationToken);

                    // Log successful step completion
                    _telemetry.LogStepEvent(StepEventType.Completed, sagaEntity.SagaId, sagaEntity.SagaType,
                        stepDef.StepType.Name, i, result, SagaLogLevel.Information,
                        $"Step completed with status: {result}");

                    _telemetry.MarkCompleted(stepActivity);
                }
                catch (Exception stepEx)
                {
                    // Log step failure
                    _telemetry.LogStepEvent(StepEventType.Failed, sagaEntity.SagaId, sagaEntity.SagaType,
                        stepDef.StepType.Name, i, SagaStepStatus.Continue, stepEx, SagaLogLevel.Error);

                    _telemetry.RecordException(stepActivity, stepEx);
                    throw; // Re-throw to trigger saga compensation
                }

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
                    await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

                    _telemetry.LogSagaEvent(SagaEventType.StatusChanged, sagaEntity.SagaId, sagaEntity.SagaType,
                        SagaStatus.Awaiting, SagaLogLevel.Information,
                        $"Saga paused - waiting for external event after step: {stepDef.StepType}");

                    return; // Return early, saga will resume later
                }

                // result was Continue, so loop will move to next step
            }

            // All steps completed successfully
            sagaEntity.Status = SagaStatus.Completed;
            sagaEntity.CurrentStepIndex = _config.Steps.Count; // mark index past the last step
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            _telemetry.LogSagaEvent(SagaEventType.Completed, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.Completed, SagaLogLevel.Information,
                $"Saga completed successfully after {_config.Steps.Count} steps");
        }

        /// <summary>
        /// Handles saga failure by initiating compensation for all executed steps.
        /// </summary>
        private async Task HandleSagaFailureAsync(SagaEntity sagaEntity, Exception ex, CancellationToken cancellationToken)
        {
            // A step threw an error – begin compensation
            sagaEntity.Status = SagaStatus.Compensating;
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            _telemetry.LogSagaEvent(SagaEventType.CompensationStarted, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.Compensating, SagaLogLevel.Warning,
                $"Starting compensation due to failure at step index: {sagaEntity.CurrentStepIndex}");

            await ExecuteCompensationAsync(sagaEntity, cancellationToken);

            // After attempting compensation of all executed steps:
            sagaEntity.Status = SagaStatus.Compensated;
            // If any compensation failed (we caught exceptions), one could mark as Failed instead.
            // For simplicity, we'll mark as Compensated since we attempted best effort rollback.
            sagaEntity.CurrentStepIndex = 0;
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            _telemetry.LogSagaEvent(SagaEventType.CompensationCompleted, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.Compensated, SagaLogLevel.Information, "Saga compensation completed");

            // Rethrow or swallow exception depending on desired behavior.
            // Here, we swallow after compensation, as the saga is considered handled (Compensated).
        }

        /// <summary>
        /// Executes compensation steps in reverse order for all previously executed steps.
        /// </summary>
        private async Task ExecuteCompensationAsync(SagaEntity sagaEntity, CancellationToken cancellationToken)
        {
            // Determine up to which step had executed (CurrentStepIndex points to the failing step index)
            int failedStepIndex = sagaEntity.CurrentStepIndex;
            // If exception was thrown, the step at failedStepIndex did not complete successfully.
            // We need to compensate all steps before it (i.e., indices 0 to failedStepIndex-1).
            int lastExecutedIndex = failedStepIndex - 1;

            // Compensate in reverse order for all executed steps
            for (int j = lastExecutedIndex; j >= 0; j--)
            {
                var stepDef = _config.Steps[j];

                try
                {
                    // Load latest context (it might have been modified by partial execution or prior compensation)
                    var context = DeserializeContext(sagaEntity.ContextData);
                    var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);

                    _telemetry.LogStepEvent(StepEventType.Started, sagaEntity.SagaId, sagaEntity.SagaType,
                        stepDef.StepType.Name, j, SagaStepStatus.Continue, SagaLogLevel.Information,
                        $"Starting compensation for step: {stepDef.StepType.Name}");

                    // TODO: Think if we need to pass the exception to the compensation step
                    // TODO: Think if we need to apply any policies during compensation
                    await step.CompensateAsync(context, cancellationToken);

                    _telemetry.LogStepEvent(StepEventType.Compensated, sagaEntity.SagaId, sagaEntity.SagaType,
                        stepDef.StepType.Name, j, SagaStepStatus.Continue, SagaLogLevel.Information,
                        $"Step compensation completed: {stepDef.StepType.Name}");

                    // Optionally update context if compensation changes it, then save
                    sagaEntity.ContextData = SerializeContext(context);
                    sagaEntity.CurrentStepIndex = j; // Update to the last compensated step index
                    await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);
                }
                catch (Exception compEx)
                {
                    // Compensation for a step failed. Log the error and continue attempting to compensate earlier steps.
                    _telemetry.LogStepEvent(StepEventType.CompensationFailed, sagaEntity.SagaId, sagaEntity.SagaType,
                        stepDef.StepType.Name, j, SagaStepStatus.Continue, compEx, SagaLogLevel.Error);

                    // (In a real system, you might want to record this in sagaEntity as well.)
                    Console.Error.WriteLine($"Compensation step {j} failed: {compEx}");
                }
            }
        }

        #endregion Core Execution Logic

        #region Resume Handlers

        /// <summary>
        /// Factory method to get the appropriate resume handler based on saga status.
        /// This uses the Strategy pattern to eliminate if-else chains.
        /// </summary>
        private Func<SagaEntity, Activity?, CancellationToken, Task> GetResumeHandler(SagaStatus status)
        {
            return status switch
            {
                SagaStatus.Awaiting => HandleAwaitingResumeAsync,
                SagaStatus.InProgress => HandleInProgressResumeAsync,
                SagaStatus.Compensating => HandleCompensatingResumeAsync,
                _ => throw new InvalidOperationException($"Cannot resume saga with status: {status}")
            };
        }

        /// <summary>
        /// Handles resuming a saga that was in Awaiting status.
        /// </summary>
        private async Task HandleAwaitingResumeAsync(SagaEntity sagaEntity, Activity? activity, CancellationToken cancellationToken)
        {
            // Saga was waiting for an external trigger, presumably the condition is now met.
            // Continue from the current step (which had returned Awaiting).
            sagaEntity.Status = SagaStatus.InProgress;
            // Increase current step index
            sagaEntity.CurrentStepIndex++;

            _telemetry.LogSagaEvent(SagaEventType.StatusChanged, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.InProgress, SagaLogLevel.Information,
                "Saga resumed from Awaiting status - continuing to next step");

            await HandleInProgressResumeAsync(sagaEntity, activity, cancellationToken);
        }

        /// <summary>
        /// Handles resuming a saga that was in InProgress status.
        /// </summary>
        private async Task HandleInProgressResumeAsync(SagaEntity sagaEntity, Activity? activity, CancellationToken cancellationToken)
        {
            await ExecuteSagaFlowAsync(sagaEntity, activity, cancellationToken);
        }

        /// <summary>
        /// Handles resuming a saga that was in Compensating status.
        /// </summary>
        private async Task HandleCompensatingResumeAsync(SagaEntity sagaEntity, Activity? activity, CancellationToken cancellationToken)
        {
            _telemetry.LogSagaEvent(SagaEventType.CompensationStarted, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.Compensating, SagaLogLevel.Information,
                "Resuming saga compensation from previous interruption");

            await ExecuteCompensationAsync(sagaEntity, cancellationToken);

            // After attempting compensation of all executed steps:
            sagaEntity.Status = SagaStatus.Compensated;
            // If any compensation failed (we caught exceptions), one could mark as Failed instead.
            // For simplicity, we'll mark as Compensated since we attempted best effort rollback.
            sagaEntity.CurrentStepIndex = 0;
            await _sagaStore.UpdateAsync(sagaEntity, cancellationToken);

            _telemetry.LogSagaEvent(SagaEventType.CompensationCompleted, sagaEntity.SagaId, sagaEntity.SagaType,
                SagaStatus.Compensated, SagaLogLevel.Information, "Saga compensation resumed and completed");
        }

        #endregion Resume Handlers

        #region Validation

        private static void ValidateSagaStatusForResume(SagaStatus status)
        {
            if (status == SagaStatus.Completed
                || status == SagaStatus.Compensated
                || status == SagaStatus.Failed
                || status == SagaStatus.NotStarted)
            {
                throw new InvalidOperationException(
                   $"Cannot resume saga with status: {status}");
            }
        }

        #endregion Validation

        #region StepExecution
        // Helper: Execute a step with its associated policy (if any)
        private Task<SagaStepStatus> ExecuteStepWithPolicyAsync(SagaStepDefinition<TContext> stepDef, TContext context, CancellationToken cancellationToken)
        {
            var step = (ISagaStep<TContext>)_provider.GetRequiredService(stepDef.StepType);

            return stepDef.PolicyExecutor is not null
              ? stepDef.PolicyExecutor.ExecuteAsync(ct => step.ExecuteAsync(context, ct), cancellationToken)
              : step.ExecuteAsync(context, cancellationToken);
        }
        #endregion StepExecution

        #region Serialization/Deserialization

        // Helper: Serialize context to JSON string
        private string SerializeContext(TContext context)
        {
            try
            {
                if (_config.ContextTypeInfo is JsonTypeInfo<TContext> typeInfo)
                {
                    return JsonSerializer.Serialize(context, typeInfo);
                }

                return JsonSerializer.Serialize(context, _config.SerializerOptions);
            }
            catch (Exception)
            {
                return JsonSerializer.Serialize(context, _config.SerializerOptions);
            }
        }

        private TContext DeserializeContext(string jsonData)
        {
            try
            {
                if (_config.ContextTypeInfo is JsonTypeInfo<TContext> typeInfo)
                {
                    return JsonSerializer.Deserialize(jsonData, typeInfo) ?? new TContext();
                }

                return JsonSerializer.Deserialize<TContext>(jsonData, _config.SerializerOptions) ?? new TContext();
            }
            catch (Exception)
            {
                return JsonSerializer.Deserialize<TContext>(jsonData, _config.SerializerOptions) ?? new TContext();
            }
        }
        #endregion Serialization/Deserialization
    }
}