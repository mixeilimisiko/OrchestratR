using OrchestratR.Core;
using System.Diagnostics;

namespace OrchestratR.Tracing
{
    /// <summary>
    /// No-op implementation of ISagaTelemetry for scenarios where tracing/logging is disabled.
    /// </summary>
    public class NoSagaTelemetry : ISagaTelemetry
    {
        #region Tracing

        public Activity? StartSaga(Guid sagaId, string sagaType, string operation) => null;

        public Activity? StartStep(Guid sagaId, string stepName, int stepIndex) => null;

        public void RecordException(Activity? activity, Exception exception) { }

        public void MarkCompleted(Activity? activity) { }

        #endregion

        #region Logging

        public void LogSagaEvent(
            SagaEventType eventType,
            Guid sagaId,
            string sagaType,
            SagaStatus status,
            SagaLogLevel level = SagaLogLevel.Information,
            string? message = null,
            string? traceId = null)
        { }

        public void LogSagaEvent(
            SagaEventType eventType,
            Guid sagaId,
            string sagaType,
            SagaStatus status,
            Exception exception,
            SagaLogLevel level = SagaLogLevel.Error,
            string? traceId = null)
        { }

        public void LogStepEvent(
            StepEventType eventType,
            Guid sagaId,
            string sagaType,
            string stepName,
            int stepIndex,
            SagaStepStatus stepStatus,
            SagaLogLevel level = SagaLogLevel.Information,
            string? message = null,
            string? traceId = null)
        { }

        public void LogStepEvent(
            StepEventType eventType,
            Guid sagaId,
            string sagaType,
            string stepName,
            int stepIndex,
            SagaStepStatus stepStatus,
            Exception exception,
            SagaLogLevel level = SagaLogLevel.Error,
            string? traceId = null)
        { }

        #endregion
    }
}
