using OrchestratR.Core;
using OrchestratR.Persistence;

namespace OrchestratR.Tests.Persistence
{
    /// <summary>
    /// Tests for InMemorySagaStore
    /// </summary>
    public class InMemorySagaStoreTests
    {
        [Fact]
        public async Task SaveAsync_ShouldSaveNewSaga()
        {
            // Arrange
            var store = new InMemorySagaStore();
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
        public async Task SaveAsync_ShouldThrowException_WhenSagaIdAlreadyExists()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await store.SaveAsync(saga);
            });
        }

        [Fact]
        public async Task FindByIdAsync_ShouldReturnNull_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = new InMemorySagaStore();
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
            var store = new InMemorySagaStore();
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
            var store = new InMemorySagaStore();
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
        public async Task UpdateAsync_ShouldAddSagaIfNotExists()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var saga = CreateTestSaga();

            // Act - no exception should be thrown
            await store.UpdateAsync(saga);

            // Assert
            var retrieved = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(retrieved);
            Assert.Equal(saga.SagaId, retrieved.SagaId);
        }

        [Fact]
        public async Task FindByStatusAsync_ShouldReturnEmptyList_WhenNoSagasMatchStatus()
        {
            // Arrange
            var store = new InMemorySagaStore();
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
            var store = new InMemorySagaStore();

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

        [Fact]
        public async Task UpdateStatusAsync_ShouldUpdateSagaStatus()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var saga = CreateTestSaga(SagaStatus.NotStarted);
            await store.SaveAsync(saga);

            // Act
            await store.UpdateStatusAsync(saga.SagaId, SagaStatus.InProgress);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(updatedSaga);
            Assert.Equal(SagaStatus.InProgress, updatedSaga.Status);
        }

        [Fact]
        public async Task UpdateStatusAsync_ShouldThrowException_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var nonExistentId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await store.UpdateStatusAsync(nonExistentId, SagaStatus.Completed);
            });
        }

        [Fact]
        public async Task UpdateStepIndexAsync_ShouldUpdateSagaStepIndex()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            await store.UpdateStepIndexAsync(saga.SagaId, 5);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(updatedSaga);
            Assert.Equal(5, updatedSaga.CurrentStepIndex);
        }

        [Fact]
        public async Task UpdateStepIndexAsync_ShouldThrowException_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var nonExistentId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await store.UpdateStepIndexAsync(nonExistentId, 3);
            });
        }

        [Fact]
        public async Task UpdateContextDataAsync_ShouldUpdateSagaContextData()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var saga = CreateTestSaga();
            await store.SaveAsync(saga);

            // Act
            var newContextData = "{\"updatedData\": \"newValue\"}";
            await store.UpdateContextDataAsync(saga.SagaId, newContextData);

            // Assert
            var updatedSaga = await store.FindByIdAsync(saga.SagaId);
            Assert.NotNull(updatedSaga);
            Assert.Equal(newContextData, updatedSaga.ContextData);
        }

        [Fact]
        public async Task UpdateContextDataAsync_ShouldThrowException_WhenSagaDoesNotExist()
        {
            // Arrange
            var store = new InMemorySagaStore();
            var nonExistentId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await store.UpdateContextDataAsync(nonExistentId, "{\"key\": \"value\"}");
            });
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
}
