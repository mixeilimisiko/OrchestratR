using DemoAPI.OrderSaga;
using DemoAPI.OrderSagaSync;
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
        private readonly SagaOrchestrator<OrderSagaSyncContext> _orchestratorSync;
        private readonly ISagaStore _sagaStore;


        public OrderController(SagaOrchestrator<OrderSagaContext> orchestrator, SagaOrchestrator<OrderSagaSyncContext> orchestratorSync, ISagaStore sagaStore)
        {
            _orchestrator = orchestrator;
            _orchestratorSync = orchestratorSync;
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

        [HttpGet("startSync")]
        public async Task<Guid> StartSagaSync(CancellationToken cancellationToken)
        {
            var newContext = new OrderSagaSyncContext { OrderId = Guid.NewGuid() };
            Console.WriteLine($"[API] Starting sync saga for order {newContext.OrderId}...");
            Guid sagaId = await _orchestratorSync.StartAsync(newContext, cancellationToken);
            Console.WriteLine($"[API] Sync saga started with ID {sagaId}");
            return sagaId;
        }

        [HttpGet("getInfoSync")]
        public async Task<SagaEntity> GetInfoSync(Guid sagaId)
        {
            var sagaEntity = await _sagaStore.FindByIdAsync(sagaId);
            Console.WriteLine($"[API] Sync saga {sagaId} status: {sagaEntity?.Status}");
            return sagaEntity ?? new SagaEntity();
        }
    }
}
