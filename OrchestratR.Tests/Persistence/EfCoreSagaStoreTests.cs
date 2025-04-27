using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrchestratR.Core;
using OrchestratR.Persistence;

namespace OrchestratR.Tests.Persistence
{
    /// <summary>
    /// Tests for EfCoreSagaStore
    /// </summary>
    public class EfCoreSagaStoreTests : IClassFixture<EfCoreSagaStoreFixture>
    {
        private readonly EfCoreSagaStoreFixture _fixture;

        public EfCoreSagaStoreTests(EfCoreSagaStoreFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SaveAsync_ShouldSaveNewSaga()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();

            // Act
            await store.SaveAsync(saga);

            // Assert
            var retrieved = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(retrieved);
            Assert.Equal(saga.SagaId, retrieved.SagaId);
            Assert.Equal(saga.SagaType, retrieved.SagaType);
            Assert.Equal(saga.Status, retrieved.Status);
        }

        [Fact]
        public async Task FindByIdAsync_ShouldReturnNull_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await store.FindByIdAsync(nonExistentId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task FindByIdAsync_ShouldReturnSaga_WhenSagaExists()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            var result = await store.FindByIdAsync(saga.SagaId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(saga.SagaId, result.SagaId);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateExistingSaga()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            saga.Status = SagaStatus.Completed;
            saga.CurrentStepIndex = 3;
            await store.UpdateAsync(saga);

            // Assert
            var updated = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(updated);
            Assert.Equal(SagaStatus.Completed, updated.Status);
            Assert.Equal(3, updated.CurrentStepIndex);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrowException_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            {
                await store.UpdateAsync(saga);
            });
        }

        [Fact]
        public async Task FindByStatusAsync_ShouldReturnEmptyList_WhenNoSagasMatchStatus()
        {
            // Arrange
            var store = _fixture.CreateStore();
            var saga = CreateTestSaga(SagaStatus.InProgress);
            await store.SaveAsync(saga);

            // Act
            var result = await store.FindByStatusAsync(SagaStatus.Completed);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task FindByStatusAsync_ShouldReturnMatchingSagas()
        {
            // Arrange
            var store = _fixture.CreateStore();

            var saga1 = CreateTestSaga(SagaStatus.InProgress);
            var saga2 = CreateTestSaga(SagaStatus.InProgress);
            var saga3 = CreateTestSaga(SagaStatus.Completed);

            await store.SaveAsync(saga1);
            await store.SaveAsync(saga2);
            await store.SaveAsync(saga3);

            // Act
            var inProgressSagas = await store.FindByStatusAsync(SagaStatus.InProgress);
            var completedSagas = await store.FindByStatusAsync(SagaStatus.Completed);

            // Assert
            Assert.Equal(2, inProgressSagas.Count);
            Assert.Single(completedSagas);
            Assert.Contains(inProgressSagas, s => s.SagaId == saga1.SagaId);
            Assert.Contains(inProgressSagas, s => s.SagaId == saga2.SagaId);
            Assert.Contains(completedSagas, s => s.SagaId == saga3.SagaId);
        }

        private SagaEntity CreateTestSaga(SagaStatus status = SagaStatus.NotStarted)
        {
            return new SagaEntity
            {
                SagaId = Guid.NewGuid(),
                SagaType = "TestSaga",
                Status = status,
                CurrentStepIndex = 0,
                ContextData = "{\"testData\": \"value\"}"
            };
        }
    }

    /// <summary>
    /// Fixture for EfCoreSagaStore tests to provide shared database context
    /// </summary>
    public class EfCoreSagaStoreFixture
    {
        public ISagaStore CreateStore()
        {
            var options = new DbContextOptionsBuilder<SagaDbContext>()
                .UseInMemoryDatabase(databaseName: $"SagaTestDb_{Guid.NewGuid()}") // New DB per test
                .Options;

            var context = new SagaDbContext(options);
            context.Database.EnsureCreated();

            return new EfCoreSagaStore(context);
        }
    }
}
