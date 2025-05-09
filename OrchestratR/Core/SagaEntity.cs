
using System.ComponentModel.DataAnnotations;

namespace OrchestratR.Core
{
    /// <summary>
    /// Persistent record of a saga's state, used by SagaStore implementations.
    /// </summary>
    public class SagaEntity
    {
        public Guid SagaId { get; set; }                // Unique identifier for the saga instance
        public string SagaType { get; set; } = "";      // Name of the saga context/type (used to route to correct orchestrator)
        public SagaStatus Status { get; set; }          // Current status of the saga
        public int CurrentStepIndex { get; set; }       // Index of the next step to execute (or current step in progress)
        public string ContextData { get; set; } = "";   // JSON serialized SagaContext

        [Timestamp]  // concurrency token
        public byte[] RowVersion { get; set; } = [];
    }
}
