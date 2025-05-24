using System.Diagnostics;

namespace OrchestratR.Tracing
{
    public class NoSagaTelemetry : ISagaTelemetry
    {
        public Activity? StartSaga(Guid sagaId, string sagaType, string operation) => null;
        public void RecordException(Activity? activity, Exception exception, string eventName = "SagaFailed") { }
        public void MarkCompleted(Activity? activity, string status = "Completed") { }
    }
}
