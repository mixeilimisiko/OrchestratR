
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchestratR.Core;
using OrchestratR.Orchestration;

namespace OrchestratR.Recovery
{
    /// <summary>
    /// Background service that recovers and resumes any sagas that were left incomplete (InProgress, Awaiting, or Compensating).
    /// </summary>
    public class SagaRecoveryService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        public SagaRecoveryService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay a bit to allow application startup to complete, if necessary
            await Task.Delay(100, stoppingToken);

            using (var scope = _serviceProvider.CreateScope())
            {
                var sagaStore = scope.ServiceProvider.GetRequiredService<ISagaStore>();
                // Retrieve all sagas in states that need recovery
                var incompleteStatuses = new[]
                {
                    SagaStatus.InProgress,
                    SagaStatus.Compensating
                };
                var sagasToResume = new List<SagaEntity>();
                foreach (var status in incompleteStatuses)
                {
                    var sagas = await sagaStore.FindByStatusAsync(status, stoppingToken);
                    sagasToResume.AddRange(sagas);
                }

                if (sagasToResume.Count <= 0)
                    return; // no sagas to recover

                // Get all orchestrators registered
                var orchestrators = scope.ServiceProvider.GetServices<ISagaOrchestrator>();

                foreach (var saga in sagasToResume)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        // Find the orchestrator whose SagaTypeName matches the saga's type
                        var orchestrator = orchestrators.FirstOrDefault(o => o.SagaTypeName == saga.SagaType);
                        if (orchestrator == null)
                        {
                            Console.Error.WriteLine($"No orchestrator found for SagaType {saga.SagaType}. Unable to resume SagaId {saga.SagaId}.");
                            continue;
                        }
                        Console.WriteLine($"Resuming saga {saga.SagaId} of type {saga.SagaType}, status {saga.Status}...");
                        await orchestrator.ResumeAsync(saga, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error resuming saga {saga.SagaId}: {ex}");
                    }
                }
            }
        }
    }
}
