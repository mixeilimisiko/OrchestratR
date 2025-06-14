
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchestratR.Persistence;

namespace OrchestratR.Registration
{
    internal class SagaMigrationService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        public SagaMigrationService(IServiceProvider serviceProvider)
            => _serviceProvider = serviceProvider;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
                try
                {
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    // Ideally log the exception. In a real library, consider injecting ILogger<SagaMigrationService>
                    // and logging the error, then perhaps rethrow or handle accordingly.
                    // For example: _logger.LogError(ex, "Failed to apply saga migrations");
                    throw;
                }
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
