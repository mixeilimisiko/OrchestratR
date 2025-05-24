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
        public async Task<Guid> StartSaga(CancellationToken cancellationToken)
        {

            var newOrderSagaContext = new OrderSagaContext { OrderId = Guid.NewGuid() };
            Console.WriteLine($"Starting saga for Order {newOrderSagaContext.OrderId}...");
            Guid sagaId = await _orchestrator.StartAsync(newOrderSagaContext, cancellationToken);

            return sagaId;
        }

        [HttpGet("callback")]
        public async Task<Guid> Continue(Guid sagaId, CancellationToken cancellation)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId);
            if (sagaEntity != null)
            {
                // Resume the saga
                await _orchestrator.ResumeAsync(sagaId, ctx => ctx.PaymentProcessed = true);
            }
            return sagaId;
        }

        [HttpGet("getInfo")]
        public async Task<SagaEntity> GetInfo(Guid sagaId)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId);
            Console.WriteLine($"Saga {sagaId} completed with status: {sagaEntity?.Status}");
            return sagaEntity ?? new SagaEntity();
        }

    }
}
