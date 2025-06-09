
using FluentAssertions.Execution;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchestratR.Core;
using OrchestratR.Orchestration;
using OrchestratR.Persistence;
using OrchestratR.Tracing;
using System.Text.Json;

namespace OrchestratR.Tests.Orchestration
{

    public class AsyncTestSagaContext : SagaContext
    {
        public bool StepACalled { get; set; }
        public bool StepACompensated { get; set; }
        public string StepAResult { get; set; } = string.Empty;
        public bool StepBCalled { get; set; }
        public bool StepBCompensated { get; set; }
    }

    public class AsyncStepA : ISagaStep<AsyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(AsyncTestSagaContext context, CancellationToken ct)
        {
            context.StepACalled = true;
            return Task.FromResult(SagaStepStatus.Awaiting);
        }

        public Task CompensateAsync(AsyncTestSagaContext context, CancellationToken ct)
        {
            context.StepACompensated = true;
            return Task.CompletedTask;
        }
    }

    public class AsyncStepB : ISagaStep<AsyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(AsyncTestSagaContext context, CancellationToken ct)
        {
            context.StepBCalled = true;
            return Task.FromResult(SagaStepStatus.Awaiting);
        }

        public Task CompensateAsync(AsyncTestSagaContext context, CancellationToken ct)
        {
            context.StepBCompensated = true;
            return Task.CompletedTask;
        }
    }

    public class SagaOrchestratorAsynchronousScenarioTests
    {
        [Fact]
        public async Task StartAsync_ShouldStopAtAsyncStepWithAwaitingStatus()
        {
            /*================================  Arrange  =========================================*/

            // 1) Set up in‐memory EF Core database
            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_HappyPath_NoTelemetry"));

            services.AddScoped<ISagaStore, EfCoreSagaStore>();

            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            services.AddSingleton<AsyncStepA>();

            services.AddSingleton<ISagaStep<AsyncTestSagaContext>>(sp => sp.GetRequiredService<AsyncStepA>());

            var config = new SagaConfig<AsyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                },
                ContextTypeInfo = null
            };

            config.Steps.Add(new SagaStepDefinition<AsyncTestSagaContext>(typeof(AsyncStepA)));

            services.AddSingleton<IOptions<SagaConfig<AsyncTestSagaContext>>>(sp =>
               Options.Create(config));

            services.AddSingleton<SagaOrchestrator<AsyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();
             
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<AsyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            /*================================  Act  =========================================*/

            var initialContext = new AsyncTestSagaContext
            {
                StepACalled = false,
                StepACompensated = false
            };

            var sagaId = await orchestrator.StartAsync(initialContext);

            /*===============================  Assert ==========================================*/

            var savedEntity = await store!.FindByIdAsync(sagaId, CancellationToken.None);
            using (new AssertionScope())
            {
                savedEntity.Should().NotBeNull();
                savedEntity!.Status.Should().Be(SagaStatus.Awaiting);
                savedEntity.CurrentStepIndex.Should().Be(0);

                var deserializedContext = JsonSerializer.Deserialize<AsyncTestSagaContext>(
                    savedEntity.ContextData!, config.SerializerOptions);
                deserializedContext.Should().NotBeNull();
                deserializedContext!.StepACalled.Should().BeTrue("SyncStepA should have set StepACalled");
            }
        }

        [Fact]
        public async Task ResumeAsync_WithPatch_UpdatesContext()
        {
            /*================================  Arrange  =========================================*/

            // 1) Set up in‐memory EF Core database
            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_HappyPath_NoTelemetry"));

            services.AddScoped<ISagaStore, EfCoreSagaStore>();

            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            services.AddSingleton<AsyncStepA>();
            services.AddSingleton<AsyncStepB>();

            services.AddSingleton<ISagaStep<AsyncTestSagaContext>>(sp => sp.GetRequiredService<AsyncStepA>());

            var config = new SagaConfig<AsyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                },
                ContextTypeInfo = null 
            };

            config.Steps.Add(new SagaStepDefinition<AsyncTestSagaContext>(typeof(AsyncStepA)));
            config.Steps.Add(new SagaStepDefinition<AsyncTestSagaContext>(typeof(AsyncStepB)));

            services.AddSingleton<IOptions<SagaConfig<AsyncTestSagaContext>>>(sp =>
               Options.Create(config));

            services.AddSingleton<SagaOrchestrator<AsyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();

            var orchestrator = provider.GetRequiredService<SagaOrchestrator<AsyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            /*================================  Act  =========================================*/

            var initialContext = new AsyncTestSagaContext
            {
                StepACalled = false,
                StepACompensated = false
            };

            var sagaId = await orchestrator.StartAsync(initialContext);

            await orchestrator.ResumeAsync(sagaId, ctx => ctx.StepAResult = "res");

            /*===============================  Assert ==========================================*/

            var savedEntity = await store!.FindByIdAsync(sagaId, CancellationToken.None);
            using (new AssertionScope())
            {
                savedEntity.Should().NotBeNull();
                savedEntity!.Status.Should().Be(SagaStatus.Awaiting);
                savedEntity.CurrentStepIndex.Should().Be(1);

                var deserializedContext = JsonSerializer.Deserialize<AsyncTestSagaContext>(
                    savedEntity.ContextData!, config.SerializerOptions);
                deserializedContext.Should().NotBeNull();
                deserializedContext!.StepACalled.Should().BeTrue("SyncStepA should have set StepACalled");
                deserializedContext!.StepAResult.Should().Be("res");
                deserializedContext!.StepBCalled.Should().BeTrue("SyncStepB should have set StepBCalled");
            }
        }
    }
}
