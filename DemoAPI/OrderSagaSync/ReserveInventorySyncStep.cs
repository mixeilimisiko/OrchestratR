using OrchestratR.Core;

namespace DemoAPI.OrderSagaSync
{
    public class ReserveInventorySyncStep : ISagaStep<OrderSagaSyncContext>
    {
        private readonly IInventoryServiceSync _inventoryService;

        public ReserveInventorySyncStep(IInventoryServiceSync inventoryService)
        {
            _inventoryService = inventoryService;
        }

        public Task<SagaStepStatus> ExecuteAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[ReserveInventorySyncStep] Executing for order {context.OrderId}...");
            // Simulate a failure trigger (set x = true to throw)
            bool x = false;
            if (x)
            {
                throw new Exception("Inventory reservation failed (sync)!");
            }

            // Perform a synchronous reservation
            var reservationId = _inventoryService.Reserve(context.OrderId);
            context.InventoryReserved = true;
            Console.WriteLine($"[ReserveInventorySyncStep] InventoryReserved=true");
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            if (context.InventoryReserved)
            {
                Console.WriteLine($"[ReserveInventorySyncStep] Compensating (releasing inventory) for order {context.OrderId}...");
            
                // For demo, just simulate release
                _inventoryService.Release(Guid.NewGuid().ToString());
                context.InventoryReserved = false;
                Console.WriteLine($"[ReserveInventorySyncStep] InventoryReserved=false");
            }
            return Task.CompletedTask;
        }
    }
}
