using OrchestratR.Core;
using System.Diagnostics;

namespace OrchestratR.Tracing
{
    public interface ISagaTelemetry
    {
        //Activity? StartSaga(Guid sagaId, string sagaType, string operation);
        //void RecordException(Activity? activity, Exception exception, string eventName = "SagaFailed");
        //void MarkCompleted(Activity? activity, string status = "Completed");

        #region Distributed Tracing (OpenTelemetry)

        /// <summary>
        /// Starts a new activity/span for saga operations.
        /// </summary>
        Activity? StartSaga(Guid sagaId, string sagaType, string operation);

        /// <summary>
        /// Starts a new activity/span for step operations.
        /// </summary>
        Activity? StartStep(Guid sagaId, string stepName, int stepIndex);

        /// <summary>
        /// Marks an activity as completed successfully.
        /// </summary>
        void MarkCompleted(Activity? activity);

        /// <summary>
        /// Records an exception in the current activity/span.
        /// </summary>
        void RecordException(Activity? activity, Exception exception);

        #endregion



        #region Structured Logging

        /// <summary>
        /// Logs saga lifecycle events (started, completed, failed, etc.)
        /// </summary>
        void LogSagaEvent(
                SagaEventType eventType,
                Guid sagaId,
                string sagaType,
                SagaStatus status,
                SagaLogLevel level = SagaLogLevel.Information,
                string? message = null,
                string? traceId = null);
        /// <summary>
        /// Logs saga lifecycle events with exceptions
        /// </summary>
        void LogSagaEvent(
          SagaEventType eventType,
          Guid sagaId,
          string sagaType,
          SagaStatus status,
          Exception exception,
          SagaLogLevel level = SagaLogLevel.Error,
          string? traceId = null);


        /// <summary>
        /// Logs step execution events
        /// </summary>

        void LogStepEvent(
            StepEventType eventType,
            Guid sagaId,
            string sagaType,
            string stepName,
            int stepIndex,
            SagaStepStatus stepStatus,
            SagaLogLevel level = SagaLogLevel.Information,
            string? message = null,
            string? traceId = null);

        /// <summary>
        /// Logs step execution events with exceptions
        /// </summary>

        void LogStepEvent(
            StepEventType eventType,
            Guid sagaId,
            string sagaType,
            string stepName,
            int stepIndex,
            SagaStepStatus stepStatus,
            Exception exception,
            SagaLogLevel level = SagaLogLevel.Error,
            string? traceId = null);
        #endregion
    }

    // possibly use this stuff for logging, or just use existing enums of sagastatus and sagastepstatus

    /// <summary>
    /// Types of saga lifecycle events for structured logging and metrics.
    /// </summary>
    public enum SagaEventType
    {
        Started,
        Resumed,
        StatusChanged,
        Completed,
        Failed,
        Cancelled,
        CompensationStarted,
        CompensationCompleted,
        ContextUpdated
    }

    /// <summary>
    /// Saga-specific log levels that map to standard logging levels
    /// but provide semantic meaning in the saga context.
    /// </summary>
    public enum SagaLogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Types of step execution events for structured logging and metrics.
    /// </summary>
    public enum StepEventType
    {
        Started,
        Completed,
        Failed,
        Compensated,
        CompensationFailed,
        Retrying,
        TimedOut
    }

}
