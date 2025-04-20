
using OrchestratR.Core;

namespace OrchestratR.Orchestration
{
    /// <summary>
    /// Orchestrator responsible for executing saga steps and managing saga state.
    /// </summary>
    /// <typeparam name="TContext">The specific SagaContext type for this saga.</typeparam>
    public class SagaOrchestrator<TContext> : ISagaOrchestrator
        where TContext : SagaContext, new()
    {
        private readonly List<ISagaStep<TContext>> _steps = new();
        private readonly ISagaStore _sagaStore;

        public string SagaTypeName { get; } = typeof(TContext).Name;

        public SagaOrchestrator(ISagaStore sagaStore)
        {
            _sagaStore = sagaStore;
        }

        /// <summary>Adds a step to the saga's execution sequence.</summary>
        public SagaOrchestrator<TContext> AddStep(ISagaStep<TContext> step)
        {
            _steps.Add(step);
            return this; // Enable chaining
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
                CurrentStepIndex = 0,
                ContextData = SerializeContext(context)
            };
            await _sagaStore.SaveAsync(sagaEntity);

            // Immediately mark as InProgress and update (since we are about to execute)
            sagaEntity.Status = SagaStatus.InProgress;
            await _sagaStore.UpdateAsync(sagaEntity);

            // 2. Execute steps in order until done, awaiting, or failure.
            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    sagaEntity.CurrentStepIndex = i;
                    // Persist the index change (so recovery knows which step we were on)
                    await _sagaStore.UpdateAsync(sagaEntity);

                    // Load context object (it might have been updated in the previous loop iteration)
                    context = DeserializeContext(sagaEntity.ContextData);

                    var step = _steps[i];
                    SagaStepStatus result = await step.ExecuteAsync(context);

                    // Save the possibly updated context after step execution
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
                sagaEntity.CurrentStepIndex = _steps.Count; // mark index past the last step
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
                        await _steps[j].CompensateAsync(context);
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
        /// Resumes an incomplete saga (e.g., Awaiting or interrupted) from a stored SagaEntity.
        /// </summary>
        public async Task ResumeAsync(Core.SagaEntity sagaEntity)
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
            else if (sagaEntity.Status == Core.SagaStatus.InProgress)
            {
                // Saga was in the middle of execution when interrupted (crash scenario).
                // We will resume from the CurrentStepIndex.
                // Possibly re-run the current step if it didn't finish, assuming idempotency.
            }
            else if (sagaEntity.Status == SagaStatus.Compensating)
            {
                // Saga was in the middle of compensation when interrupted.
                // We will continue compensating remaining steps.
            }

            // Resume forward execution if applicable
            if (sagaEntity.Status == SagaStatus.InProgress)
            {
                try
                {
                    // Start from the current step index
                    for (int i = sagaEntity.CurrentStepIndex; i < _steps.Count; i++)
                    {
                        sagaEntity.CurrentStepIndex = i;
                        await _sagaStore.UpdateAsync(sagaEntity);

                        // Ensure we have latest context (could have been modified externally or earlier)
                        context = DeserializeContext(sagaEntity.ContextData);
                        var step = _steps[i];
                        SagaStepStatus result = await step.ExecuteAsync(context);
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
                    sagaEntity.CurrentStepIndex = _steps.Count;
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
                            await _steps[j].CompensateAsync(context);
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
                if (lastIndexToCompensate < 0) lastIndexToCompensate = _steps.Count - 1;
                for (int j = lastIndexToCompensate; j >= 0; j--)
                {
                    try
                    {
                        context = DeserializeContext(sagaEntity.ContextData);
                        await _steps[j].CompensateAsync(context);
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

        // Helper: Serialize context to JSON string
        private string SerializeContext(TContext context)
        {
            // Use System.Text.Json with default options for simplicity
            return System.Text.Json.JsonSerializer.Serialize<TContext>(context);
        }

        // Helper: Deserialize context from JSON string
        private TContext DeserializeContext(string jsonData)
        {
            return System.Text.Json.JsonSerializer.Deserialize<TContext>(jsonData) ?? new TContext();
        }
    }

}
