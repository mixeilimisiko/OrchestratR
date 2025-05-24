using System.Diagnostics;

namespace OrchestratR.Tracing
{
 
    public class SagaTelemetry : ISagaTelemetry
    {
        private readonly ActivitySource _activitySource;

        public SagaTelemetry()
        {
            _activitySource = SagaDiagnostics.ActivitySource;
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

        public void RecordException(Activity? activity, Exception ex, string eventName = "SagaFailed")
        {
            if (activity == null) return;

            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.AddEvent(new ActivityEvent(eventName, tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().Name },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.StackTrace ?? string.Empty }
        }));
        }

        public void MarkCompleted(Activity? activity, string status = "Completed")
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddEvent(new ActivityEvent($"Saga{status}"));
        }
    }
}
