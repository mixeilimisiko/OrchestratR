using OrchestratR.Core;

namespace DemoAPI.OrderSagaSync
{
    public class ShipOrderSyncStep : ISagaStep<OrderSagaSyncContext>
    {
        private readonly IShippingServiceSync _shippingService;

        public ShipOrderSyncStep(IShippingServiceSync shippingService)
        {
            _shippingService = shippingService;
        }

        public Task<SagaStepStatus> ExecuteAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[ShipOrderSyncStep] Executing for order {context.OrderId}...");
            // Simulate a failure trigger (set x = true to throw)
            bool x = false;
            if (x)
            {
                throw new Exception("Shipment failed (sync)!");
            }

            // Perform a synchronous shipping
            var shipmentId = _shippingService.Ship(context.OrderId);
            context.Shipped = true;
            Console.WriteLine($"[ShipOrderSyncStep] Shipped=true");
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            if (context.Shipped)
            {
                Console.WriteLine($"[ShipOrderSyncStep] Compensating (cancelling shipment) for order {context.OrderId}...");
                _shippingService.Cancel(Guid.NewGuid().ToString());
                context.Shipped = false;
                Console.WriteLine($"[ShipOrderSyncStep] Shipped=false");
            }
            return Task.CompletedTask;
        }
    }
}
