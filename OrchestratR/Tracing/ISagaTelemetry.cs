using System.Diagnostics;

namespace OrchestratR.Tracing
{
    public interface ISagaTelemetry
    {
        Activity? StartSaga(Guid sagaId, string sagaType, string operation);
        void RecordException(Activity? activity, Exception exception, string eventName = "SagaFailed");
        void MarkCompleted(Activity? activity, string status = "Completed");
    }
}
