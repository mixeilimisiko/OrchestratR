using DemoAPI.OrderSaga;
using Microsoft.AspNetCore.Mvc;
using OrchestratR.Core;
using OrchestratR.Orchestration;

namespace DemoAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {

        private readonly SagaOrchestrator<OrderSagaContext> _orchestrator;
        private readonly ISagaStore _sagaStore;

        public OrderController(SagaOrchestrator<OrderSagaContext> orchestrator, ISagaStore sagaStore)
        {
            _orchestrator = orchestrator;
            _sagaStore = sagaStore;
        }

        [HttpGet("start")]
        public async Task<Guid> StartSaga()
        {

            var newOrderSagaContext = new OrderSagaContext { OrderId = Guid.NewGuid() };
            Console.WriteLine($"Starting saga for Order {newOrderSagaContext.OrderId}...");
            Guid sagaId = await _orchestrator.StartAsync(newOrderSagaContext);

            return sagaId;
            return await Task.FromResult(new Guid());
        }

        [HttpGet("callback")]
        public async Task<Guid> Continue(Guid sagaId)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId);
            if (sagaEntity != null)
            {
                // Update context: payment succeeded
                var context = System.Text.Json.JsonSerializer.Deserialize<OrderSagaContext>(sagaEntity.ContextData)!;
                context.PaymentProcessed = true;
                sagaEntity.ContextData = System.Text.Json.JsonSerializer.Serialize(context);
                // Optionally, update sagaEntity in store (not strictly needed until resume)
                await _sagaStore.UpdateAsync(sagaEntity);
                // Resume the saga
                var orchInterface = _orchestrator; // Use the injected orchestrator directly
                await orchInterface.ResumeAsync(sagaEntity);
            }
            return sagaId;
            return await Task.FromResult(new Guid());
        }

        [HttpGet("getInfo")]
        public async Task<SagaEntity> GetInfo(Guid sagaId)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId);
            Console.WriteLine($"Saga {sagaId} completed with status: {sagaEntity?.Status}");
            return sagaEntity;
        }

    }
}
