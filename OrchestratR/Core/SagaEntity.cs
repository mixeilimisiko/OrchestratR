
using System.ComponentModel.DataAnnotations;

namespace OrchestratR.Core
{
    /// <summary>
    /// Persistent record of a saga's state, used by <see cref="ISagaStore"/> implementations.
    /// </summary>
    public class SagaEntity
    {
        /// <summary>
        /// Unique identifier for the saga instance.
        /// </summary>
        public Guid SagaId { get; set; }

        /// <summary>
        /// The fully qualified name of the saga context/type.
        /// Used to route to the correct orchestrator implementation.
        /// </summary>
        public string SagaType { get; set; } = string.Empty;

        /// <summary>
        /// Current execution status of the saga.
        /// </summary>
        public SagaStatus Status { get; set; }

        /// <summary>
        /// Index of the next step to execute, or the step currently in progress.
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// JSON-serialized representation of the <see cref="SagaContext"/>.
        /// </summary>
        public string ContextData { get; set; } = string.Empty;

        /// <summary>
        /// Concurrency token used to detect conflicting updates.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
