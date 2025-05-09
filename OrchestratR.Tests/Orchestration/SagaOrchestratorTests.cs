
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchestratR.Core;
using OrchestratR.Orchestration;
using OrchestratR.Persistence;

namespace OrchestratR.Tests.Orchestration
{
    public class SagaOrchestratorTests
    {

        [Fact]
        public async void TestNothing()
        {

            var services = new ServiceCollection();

            services.AddTransient<TestStep>();

            var sagaStore = new InMemorySagaStore();
            services.AddSingleton<ISagaStore>(sagaStore);
            var config = new SagaConfig<TestSagaContext>
            {
                Steps = new()
                {
                    new SagaStepDefinition<TestSagaContext>(typeof(TestStep))
                }
            };
            services.Configure<SagaConfig<TestSagaContext>>(opts =>
            {
                opts.Steps = config.Steps;
            });

            var provider = services.BuildServiceProvider();
            var orchestrator = new SagaOrchestrator<TestSagaContext>(
                provider.GetRequiredService<IOptions<SagaConfig<TestSagaContext>>>(),
                provider,
                provider.GetRequiredService<ISagaStore>()
            );

            var context = new TestSagaContext { TestProperty = "Hello" };

            // Act
            var sagaId = await orchestrator.StartAsync(context);

            // Assert
            var saved = await sagaStore.FindByIdAsync(sagaId);
            Assert.NotNull(saved);
            Assert.Equal(SagaStatus.Completed, saved!.Status);

            Assert.True(true);
        }

        private class TestSagaContext : SagaContext
        {
            public string TestProperty { get; set; } = string.Empty;
        }

        private class TestStep : ISagaStep<TestSagaContext>
        {
            public Task<SagaStepStatus> ExecuteAsync(TestSagaContext context)
            {
                // Simulate work
                return Task.FromResult(SagaStepStatus.Continue);
            }

            public Task CompensateAsync(TestSagaContext context)
            {
                // No-op
                return Task.CompletedTask;
            }
        }

    }
}
