
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchestratR.Core;
using OrchestratR.Orchestration;
using OrchestratR.Persistence;
using OrchestratR.Tracing;
using System.Text.Json;
using FluentAssertions.Execution;

namespace OrchestratR.Tests.Orchestration
{
    // 1) Extend SagaContext to carry step‐execution flags
    public class SyncTestSagaContext : SagaContext
    {
        public bool StepACalled { get; set; }
        public bool StepACompensated { get; set; }
        public bool StepBCalled { get; set; }
        public bool StepCCalled { get; set; }
        public int FlakyStepCallCount { get; set; }
    }

    public class SyncStepA : ISagaStep<SyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(SyncTestSagaContext context, CancellationToken ct)
        {
            context.StepACalled = true;
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(SyncTestSagaContext context, CancellationToken ct)
        {
            context.StepACompensated = true;
            return Task.CompletedTask;
        }
    }

    public class SyncStepB : ISagaStep<SyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(SyncTestSagaContext context, CancellationToken ct)
        {
            context.StepBCalled = true;
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(SyncTestSagaContext context, CancellationToken ct)
            => Task.CompletedTask;
    }

    public class SyncStepC : ISagaStep<SyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(SyncTestSagaContext context, CancellationToken ct)
            => throw new InvalidOperationException("Step C failed");

        public Task CompensateAsync(SyncTestSagaContext context, CancellationToken ct)
            => Task.CompletedTask;
    }

    public class SyncStepD : ISagaStep<SyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(SyncTestSagaContext context, CancellationToken ct)
            => throw new OperationCanceledException("Step D canceled");

        public Task CompensateAsync(SyncTestSagaContext context, CancellationToken ct)
            => Task.CompletedTask;
    }

    public class FlakyStep : ISagaStep<SyncTestSagaContext>
    {
        public Task<SagaStepStatus> ExecuteAsync(SyncTestSagaContext ctx, CancellationToken ct)
        {
            ctx.FlakyStepCallCount++;
            if (ctx.FlakyStepCallCount <= 2)
                throw new InvalidOperationException("Transient failure");
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(SyncTestSagaContext ctx, CancellationToken ct)
            => Task.CompletedTask;
    }



    public class SagaOrchestratorSynchronousScenarioTests
    {
        [Fact]
        public async Task StartAsync_AllStepsUpdateContextAndSagaCompletes()
        {
            /*================================  Arrange  =========================================*/

            // 1) Set up in‐memory EF Core database
            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_HappyPath_NoTelemetry"));

            // 2) Register the EF‐based saga store
            services.AddScoped<ISagaStore, EfCoreSagaStore>();

            // 3) Use NoSagaTelemetry (no-op)
            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            // 4) Register the two fake steps
            services.AddSingleton<SyncStepA>();
            services.AddSingleton<SyncStepB>();

            // Also register them as ISagaStep<TestSagaContext>
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepA>());
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepB>());

            // 5) Build a SagaConfig<TestSagaContext> with serialization configured
            var config = new SagaConfig<SyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                },
                ContextTypeInfo = null // using default JSON serialization
            };
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepA)));
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepB)));

            services.AddSingleton<IOptions<SagaConfig<SyncTestSagaContext>>>(sp =>
                Options.Create(config));

            // 6) Register the orchestrator
            services.AddSingleton<SagaOrchestrator<SyncTestSagaContext>>();

            // 7) Build
            var provider = services.BuildServiceProvider();

            // Dependencies to inspect after execution
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<SyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            /*================================  Act  =========================================*/

            var initialContext = new SyncTestSagaContext
            {
                StepACalled = false,
                StepBCalled = false
            };
            Guid sagaId = await orchestrator.StartAsync(initialContext);

            /*===============================  Assert ==========================================*/

            // 1) Load SagaEntity from EF Core
            var savedEntity = await store!.FindByIdAsync(sagaId, CancellationToken.None);
            using (new AssertionScope())
            {
                savedEntity.Should().NotBeNull();
                savedEntity!.Status.Should().Be(SagaStatus.Completed);
                savedEntity.CurrentStepIndex.Should().Be(2);

                // 2) Deserialize the context from savedEntity.ContextData
                var deserializedContext = JsonSerializer.Deserialize<SyncTestSagaContext>(
                    savedEntity.ContextData!, config.SerializerOptions);
                deserializedContext.Should().NotBeNull();
                deserializedContext!.StepACalled.Should().BeTrue("SyncStepA should have set StepACalled");
                deserializedContext.StepBCalled.Should().BeTrue("SyncStepB should have set StepBCalled");
            }
        }

        [Fact]
        public async Task StartAsync_WhenSecondStepThrows_FirstStepGetsCompensated()
        {
            /*================================  Arrange  =========================================*/

            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_CompensationFlow"));

            services.AddScoped<ISagaStore, EfCoreSagaStore>();
            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            // Register step A and step B
            services.AddSingleton<SyncStepA>();
            services.AddSingleton<SyncStepC>();
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepA>());
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepC>());

            // Build SagaConfig with two steps: A then B
            var config = new SagaConfig<SyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                ContextTypeInfo = null
            };
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepA)));
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepC)));

            services.AddSingleton<IOptions<SagaConfig<SyncTestSagaContext>>>(sp =>
                Options.Create(config));

            services.AddSingleton<SagaOrchestrator<SyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<SyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            /*================================  Act  =========================================*/

            var initialContext = new SyncTestSagaContext
            {
                StepACalled = false,
                StepACompensated = false
            };
            Guid sagaId = await orchestrator.StartAsync(initialContext);

            /*===============================  Assert ==========================================*/

            var savedEntity = await store!.FindByIdAsync(sagaId, CancellationToken.None);
            savedEntity.Should().NotBeNull();
            var deserializedContext = JsonSerializer.Deserialize<SyncTestSagaContext>(
                savedEntity.ContextData!, config.SerializerOptions);
            using (new AssertionScope())
            {
                // Saga status should be “Compensated”
                savedEntity.Status.Should().Be(SagaStatus.Compensated);

                // CurrentStepIndex reset to 0 after compensation
                savedEntity.CurrentStepIndex.Should().Be(0);

                deserializedContext.Should().NotBeNull();
                deserializedContext!.StepACalled.Should().BeTrue("Step A must have executed before B failed");
                deserializedContext.StepACompensated
                    .Should().BeTrue("Step A’s CompensateAsync should have been invoked after B threw");
            }
        }


        [Fact]
        public async Task ResumeAsync_WithSeededAwaitingSaga_ContinuesToCompletion()
        {
            /*================================  Arrange  =========================================*/

            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_ResumeSeeded"));

            services.AddScoped<ISagaStore, EfCoreSagaStore>();
            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            // Register the three steps so orchestrator can resolve them by type
            services.AddSingleton<SyncStepA>();
            services.AddSingleton<SyncStepB>();
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepA>());
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepB>());

            var config = new SagaConfig<SyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                ContextTypeInfo = null
            };
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepA)));
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepB)));

            services.AddSingleton<IOptions<SagaConfig<SyncTestSagaContext>>>(sp =>
                Options.Create(config));

            services.AddSingleton<SagaOrchestrator<SyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<SyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            // Manually seed a SagaEntity as if it had run steps A and B, then awaited
            var sagaId = Guid.NewGuid();
            var midContext = new SyncTestSagaContext
            {
                StepACalled = true,
                StepBCalled = false,
            };
            var midContextJson = JsonSerializer.Serialize(midContext, config.SerializerOptions);

            var seededEntity = new SagaEntity
            {
                SagaId = sagaId,
                SagaType = typeof(SyncTestSagaContext).Name,
                Status = SagaStatus.InProgress,
                CurrentStepIndex = 1,            // B is index 1 (Awaiting returned)
                ContextData = midContextJson
            };
            await store!.SaveAsync(seededEntity, CancellationToken.None);

            /*================================  Act  =========================================*/

            await orchestrator.ResumeAsync(sagaId, CancellationToken.None);

            /*===============================  Assert  =======================================*/

            var finalEntity = await store.FindByIdAsync(sagaId, CancellationToken.None);
            finalEntity.Should().NotBeNull();
            finalEntity.Status.Should().Be(SagaStatus.Completed);

            var finalContext = JsonSerializer.Deserialize<SyncTestSagaContext>(
                finalEntity.ContextData!, config.SerializerOptions);

            using (new AssertionScope())
            {
                finalEntity!.Status.Should().Be(SagaStatus.Completed);
                finalEntity.CurrentStepIndex.Should().Be(2); // past last index

                finalContext.Should().NotBeNull();
                finalContext!.StepACalled.Should().BeTrue();
                finalContext.StepBCalled.Should().BeTrue();
            }
        }

        [Fact]
        public async Task StartAsync_WhenStepThrowsOperationCanceled_DoesNotCompensate()
        {
            /*================================  Arrange  =========================================*/

            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(opts =>
                opts.UseInMemoryDatabase("SagaOrchestrator_CancellationFlow"));

            services.AddScoped<ISagaStore, EfCoreSagaStore>();
            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            // Register step A and step B
            services.AddSingleton<SyncStepA>();
            services.AddSingleton<SyncStepD>();
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepA>());
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<SyncStepD>());

            // Build SagaConfig: A → B(cancel)
            var config = new SagaConfig<SyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                ContextTypeInfo = null
            };
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepA)));
            config.Steps.Add(new SagaStepDefinition<SyncTestSagaContext>(typeof(SyncStepD)));

            services.AddSingleton<IOptions<SagaConfig<SyncTestSagaContext>>>(sp =>
                Options.Create(config));

            services.AddSingleton<SagaOrchestrator<SyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<SyncTestSagaContext>>();
            var store = provider.GetRequiredService<ISagaStore>() as EfCoreSagaStore;

            /*================================  Act  =========================================*/

            var initialContext = new SyncTestSagaContext
            {
                StepACalled = false,
                StepACompensated = false,
            };
            Guid sagaId = await orchestrator.StartAsync(initialContext);

            /*===============================  Assert ==========================================*/

            var savedEntity = await store!.FindByIdAsync(sagaId, CancellationToken.None);
            savedEntity.Should().NotBeNull();

            var deserializedContext = JsonSerializer.Deserialize<SyncTestSagaContext>(
                savedEntity.ContextData!, config.SerializerOptions);

            using (new AssertionScope())
            {
                // Saga should remain InProgress (no compensation)
                savedEntity!.Status.Should().Be(SagaStatus.InProgress,
                    "when an OperationCanceledException occurs, orchestrator leaves saga InProgress");

                // CurrentStepIndex should be at index of C (1)
                savedEntity.CurrentStepIndex.Should().Be(1);

                deserializedContext.Should().NotBeNull();
                deserializedContext!.StepACalled.Should().BeTrue("Step A ran before cancellation");
                deserializedContext.StepACompensated.Should().BeFalse("Compensation should not run on cancellation");
            }
        }

        [Fact]
        public async Task StartAsync_WithRetryPolicy_RetriesUntilSuccess()
        {
            /*================================  Arrange  =========================================*/

            var dbName = $"RetryPolicyDB_{Guid.NewGuid()}";
            var services = new ServiceCollection();
            services.AddDbContext<SagaDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddScoped<ISagaStore, EfCoreSagaStore>();
            services.AddSingleton<ISagaTelemetry, NoSagaTelemetry>();

            // Register FlakyStep
            services.AddSingleton<FlakyStep>();
            services.AddSingleton<ISagaStep<SyncTestSagaContext>>(sp => sp.GetRequiredService<FlakyStep>());

            // Build a Polly retry executor: 2 retries
            var retryExecutor = new PollyStepPolicyExecutor<SyncTestSagaContext>(
                maxRetries: 2,
                timeout: null
                );

            // Configure saga to use FlakyStep with that executor
            var cfg = new SagaConfig<SyncTestSagaContext>
            {
                SerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ContextTypeInfo = null
            };
            var stepDef = new SagaStepDefinition<SyncTestSagaContext>(typeof(FlakyStep))
            {
                PolicyExecutor = retryExecutor
            };
            cfg.Steps.Add(stepDef);
            services.AddSingleton<IOptions<SagaConfig<SyncTestSagaContext>>>(sp => Options.Create(cfg));

            services.AddSingleton<SagaOrchestrator<SyncTestSagaContext>>();

            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<SagaOrchestrator<SyncTestSagaContext>>();
            var store = (EfCoreSagaStore)provider.GetRequiredService<ISagaStore>();

            /*================================  Act  =========================================*/

            var ctx = new SyncTestSagaContext();
            var sagaId = await orchestrator.StartAsync(ctx);

            /*===============================  Assert ==========================================*/

            // After completion, the context saved in the store should reflect 3 calls
            var entity = await store.FindByIdAsync(sagaId, CancellationToken.None);
            var finalCtx = JsonSerializer.Deserialize<SyncTestSagaContext>(
                entity.ContextData!, cfg.SerializerOptions);

            finalCtx!.FlakyStepCallCount
                .Should().Be(3, "initial call + 2 retries = 3 total executions");
            entity.Status.Should().Be(SagaStatus.Completed);
        }
    }
}
