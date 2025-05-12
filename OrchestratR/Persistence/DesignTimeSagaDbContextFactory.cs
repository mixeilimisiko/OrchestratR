using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace OrchestratR.Persistence
{
    public class DesignTimeSagaDbContextFactory : IDesignTimeDbContextFactory<SagaDbContext>
    {
        public SagaDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SagaDbContext>()
                .UseSqlServer("Server=.;Database=SagaDbDesignTime;Trusted_Connection=True;")
                .Options;

            return new SagaDbContext(options);
        }
    }
}
