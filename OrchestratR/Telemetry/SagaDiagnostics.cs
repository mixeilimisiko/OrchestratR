
using System.Diagnostics;

namespace OrchestratR.Tracing
{
    public static class SagaDiagnostics
    {
        public static readonly ActivitySource ActivitySource = new("OrchestratR.SagaOrchestrator");
    }
}
