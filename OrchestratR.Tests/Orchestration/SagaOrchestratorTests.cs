
using OrchestratR.Core;
using OrchestratR.Orchestration;
using OrchestratR.Persistence;

namespace OrchestratR.Tests.Orchestration
{
    public class SagaOrchestratorTests
    {

        [Fact]
        public void TestNothing()
        {
            var sagaStore = new InMemorySagaStore();
            var orchestrator = new SagaOrchestrator<TestSagaContext>(sagaStore);

            Assert.True(true);
        }


        private class TestSagaContext : SagaContext
        {
            public string TestProperty { get; set; } = string.Empty;
        }
    }
}
