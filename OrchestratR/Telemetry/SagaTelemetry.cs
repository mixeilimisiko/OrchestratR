using Microsoft.Extensions.Logging;
using OrchestratR.Core;
using System.Diagnostics;

namespace OrchestratR.Tracing
{
    public class SagaTelemetry : ISagaTelemetry
    {
        private readonly ActivitySource _activitySource;
        private readonly ILogger<SagaTelemetry> _logger;

        public SagaTelemetry(ILogger<SagaTelemetry> logger)
        {
            _activitySource = SagaDiagnostics.ActivitySource;
            _logger = logger;
        }

        public Activity? StartSaga(Guid sagaId, string sagaType, string operation)
        {
            var activity = _activitySource.StartActivity($"Saga:{sagaType}:{operation}", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("saga.id", sagaId);
                activity.SetTag("saga.type", sagaType);
                activity.AddEvent(new ActivityEvent($"Saga{operation}Started"));
            }
            return activity;
        }

        public Activity? StartStep(Guid sagaId, string stepName, int stepIndex)
        {
            var activity = _activitySource.StartActivity($"Step:{stepName}", ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("saga.id", sagaId);
                activity.SetTag("saga.step.name", stepName);
                activity.SetTag("saga.step.index", stepIndex);
                activity.AddEvent(new ActivityEvent("StepStarted"));
            }
            return activity;
        }

        public void RecordException(Activity? activity, Exception exception)
        {
            if (activity is null) return;

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddEvent(new ActivityEvent("Exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().Name },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.StackTrace ?? string.Empty }
            }));
        }

        public void MarkCompleted(Activity? activity)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddEvent(new ActivityEvent("Completed"));
        }

        public void LogSagaEvent(
            SagaEventType eventType,
            Guid sagaId,
            string sagaType,
            SagaStatus status,
            SagaLogLevel level = SagaLogLevel.Information,
            string? message = null,
            string? traceId = null)
        {
            var finalTraceId = traceId ?? Activity.Current?.TraceId.ToString();
            var finalMessage = message ?? $"Saga {eventType}: {sagaId}";

            LogByLevel(level, eventType, sagaId, sagaType, status, finalMessage, finalTraceId, null);
        }

        public void LogSagaEvent(
            SagaEventType eventType,
            Guid sagaId,
            string sagaType,
            SagaStatus status,
            Exception exception,
            SagaLogLevel level = SagaLogLevel.Error,
            string? traceId = null)
        {
            var finalTraceId = traceId ?? Activity.Current?.TraceId.ToString();
            var finalMessage = $"Saga {eventType} with error: {exception.Message}";

            LogByLevel(level, eventType, sagaId, sagaType, status, finalMessage, finalTraceId, exception);
        }

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
        {
            var finalTraceId = traceId ?? Activity.Current?.TraceId.ToString();
            var finalMessage = message ?? $"Step {stepName} {eventType}";

            LogStepByLevel(level, eventType, sagaId, sagaType, stepName, stepIndex, stepStatus, finalMessage, finalTraceId, null);
        }

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
        {
            var finalTraceId = traceId ?? Activity.Current?.TraceId.ToString();
            var finalMessage = $"Step {stepName} {eventType} with error: {exception.Message}";

            LogStepByLevel(level, eventType, sagaId, sagaType, stepName, stepIndex, stepStatus, finalMessage, finalTraceId, exception);
        }

        #region Saga Event LoggerMessage Delegates

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaTrace =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Trace,
                new EventId(1001, "SagaTrace"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaDebug =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Debug,
                new EventId(1002, "SagaDebug"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaInformation =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Information,
                new EventId(1003, "SagaInformation"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaWarning =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Warning,
                new EventId(1004, "SagaWarning"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaError =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Error,
                new EventId(1005, "SagaError"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        private static readonly Action<ILogger, string, SagaEventType, Guid, string, SagaStatus, string?, Exception?> _logSagaCritical =
            LoggerMessage.Define<string, SagaEventType, Guid, string, SagaStatus, string?>(
                LogLevel.Critical,
                new EventId(1006, "SagaCritical"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, Status: {Status}, TraceId: {TraceId}");

        #endregion

        #region Step Event LoggerMessage Delegates

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepTrace =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Trace,
                new EventId(2001, "StepTrace"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepDebug =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Debug,
                new EventId(2002, "StepDebug"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepInformation =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Information,
                new EventId(2003, "StepInformation"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepWarning =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Warning,
                new EventId(2004, "StepWarning"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepError =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Error,
                new EventId(2005, "StepError"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        private static readonly Action<ILogger, string, StepEventType, Guid, string, string, Exception?> _logStepCritical =
            LoggerMessage.Define<string, StepEventType, Guid, string, string>(
                LogLevel.Critical,
                new EventId(2006, "StepCritical"),
                "{Message} - Event: {EventType}, SagaId: {SagaId}, SagaType: {SagaType}, StepDetails: {StepDetails}");

        #endregion

        #region Private Helper Methods

        private void LogByLevel(
            SagaLogLevel level,
            SagaEventType eventType,
            Guid sagaId,
            string sagaType,
            SagaStatus status,
            string message,
            string? traceId,
            Exception? exception)
        {
            switch (level)
            {
                case SagaLogLevel.Trace:
                    _logSagaTrace(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
                case SagaLogLevel.Debug:
                    _logSagaDebug(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
                case SagaLogLevel.Information:
                    _logSagaInformation(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
                case SagaLogLevel.Warning:
                    _logSagaWarning(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
                case SagaLogLevel.Error:
                    _logSagaError(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
                case SagaLogLevel.Critical:
                    _logSagaCritical(_logger, message, eventType, sagaId, sagaType, status, traceId, exception);
                    break;
            }
        }

        private void LogStepByLevel(
            SagaLogLevel level,
            StepEventType eventType,
            Guid sagaId,
            string sagaType,
            string stepName,
            int stepIndex,
            SagaStepStatus stepStatus,
            string message,
            string? traceId,
            Exception? exception)
        {
            // Combine step details into a single structured string to stay within LoggerMessage parameter limits
            var stepDetails = $"Name={stepName}, Index={stepIndex}, Status={stepStatus}, TraceId={traceId}";

            switch (level)
            {
                case SagaLogLevel.Trace:
                    _logStepTrace(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
                case SagaLogLevel.Debug:
                    _logStepDebug(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
                case SagaLogLevel.Information:
                    _logStepInformation(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
                case SagaLogLevel.Warning:
                    _logStepWarning(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
                case SagaLogLevel.Error:
                    _logStepError(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
                case SagaLogLevel.Critical:
                    _logStepCritical(_logger, message, eventType, sagaId, sagaType, stepDetails, exception);
                    break;
            }
        }

        #endregion
    }
}