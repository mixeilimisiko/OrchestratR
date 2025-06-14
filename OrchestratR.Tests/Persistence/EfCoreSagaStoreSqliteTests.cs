
using FluentAssertions.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrchestratR.Core;
using OrchestratR.Persistence;

namespace OrchestratR.Tests.Persistence
{
    /// <summary>
    /// Tests for EfCoreSagaStore using SQLite In-Memory provider, focused on partial updates (ExecuteUpdateAsync).
    /// </summary>
    public class EfCoreSagaStoreSqliteTests : IClassFixture<EfCoreSagaStoreSqliteFixture>
    {
        private readonly EfCoreSagaStoreSqliteFixture _fixture;

        public EfCoreSagaStoreSqliteTests(EfCoreSagaStoreSqliteFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task UpdateStatusAsync_ShouldUpdateSagaStatus()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            await store.UpdateStatusAsync(saga.SagaId, SagaStatus.InProgress);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            using (new AssertionScope())
            {
                Assert.NotNull(updatedSaga);
                Assert.Equal(SagaStatus.InProgress, updatedSaga.Status);
                Assert.Equal(0, updatedSaga.CurrentStepIndex);
                Assert.Equal(saga.ContextData, updatedSaga.ContextData);
            }
        }

        [Fact]
        public async Task UpdateStepIndexAsync_ShouldUpdateCurrentStepIndex()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            await store.UpdateStepIndexAsync(saga.SagaId, 5);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            using (new AssertionScope())
            {
                Assert.NotNull(updatedSaga);
                Assert.Equal(5, updatedSaga.CurrentStepIndex);
                Assert.Equal(SagaStatus.NotStarted, updatedSaga.Status);
                Assert.Equal(saga.ContextData, updatedSaga.ContextData); 
            }         
        }

        [Fact]
        public async Task UpdateContextDataAsync_ShouldUpdateContextData()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            var newContextData = "{\"updated\":true}";

            // Act
            await store.UpdateContextDataAsync(saga.SagaId, newContextData);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            using (new AssertionScope())
            {
                Assert.NotNull(updatedSaga);
                Assert.Equal(newContextData, updatedSaga.ContextData);
                Assert.Equal(SagaStatus.NotStarted, updatedSaga.Status);
                Assert.Equal(0, updatedSaga.CurrentStepIndex);
            }
         
        }

        private SagaEntity CreateTestSaga(SagaStatus status = SagaStatus.NotStarted)
        {
            return new SagaEntity
            {
                SagaId = Guid.NewGuid(),
                SagaType = "TestSaga",
                Status = status,
                CurrentStepIndex = 0,
                ContextData = "{\"testData\": \"value\"}",
                RowVersion = [0x01, 0x02, 0x03, 0x04, 0x01, 0x01, 0x01, 0x01]
            };
        }
    }

    /// <summary>
    /// Fixture for EfCoreSagaStore tests using SQLite in-memory database.
    /// </summary>
    public class EfCoreSagaStoreSqliteFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SagaDbContext _dbContext;

        public EfCoreSagaStoreSqliteFixture()
        {
            //  shared SQLite connection
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open(); // Important: Must open manually

            var options = new DbContextOptionsBuilder<SagaDbContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new TestSagaDbContext(options);
            _dbContext.Database.EnsureCreated();
        }

        public ISagaStore CreateStore()
        {
            return new EfCoreSagaStore(_dbContext);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }

    public class TestSagaDbContext : SagaDbContext
    {
        public TestSagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Override the RowVersion configuration for SQLite
            modelBuilder.Entity<SagaEntity>()
                .Property(e => e.RowVersion)
                .HasDefaultValue(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        }
    }
}
