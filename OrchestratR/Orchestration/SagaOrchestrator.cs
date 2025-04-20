
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
        private readonly List<ISagaStep<TContext>> _steps = [];
        private readonly ISagaStore _sagaStore;

        public string SagaTypeName { get; } = typeof(TContext).Name;

        public SagaOrchestrator(Core.ISagaStore sagaStore)
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

            // 2. Execute steps in order until done, awaiting, or failure.

            // 3. If a step threw an error – begin compensation

            return new Guid(); // Placeholder for the new SagaId

        }

        /// <summary>
        /// Resumes an incomplete saga (e.g., Awaiting or interrupted) from a stored SagaEntity.
        /// </summary>
        public async Task ResumeAsync(SagaEntity sagaEntity)
        {
           
        }

    }

}
