using OrchestratR.Core;

namespace DemoAPI.OrderSaga
{
    public class ReserveInventoryStep : ISagaStep<OrderSagaContext>
    {
        public async Task<SagaStepStatus> ExecuteAsync(OrderSagaContext context, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Reserving inventory for order {context.OrderId}...");
            // Simulate inventory reservation (call inventory service)
            SimulateFailure();
            await Task.Delay(50);
            context.InventoryReserved = true;
            Console.WriteLine("Inventory reserved.");
            return SagaStepStatus.Continue;
        }

        public async Task CompensateAsync(OrderSagaContext context, CancellationToken cancellationToken)
        {
            if (context.InventoryReserved)
            {
                Console.WriteLine($"Releasing reserved inventory for order {context.OrderId}...");
                await Task.Delay(50);
                context.InventoryReserved = false;
                Console.WriteLine("Inventory released.");
            }
        }

        private void SimulateFailure()
        {
            // Simulate a failure in the inventory reservation step in debugging mode by manually changing value of x
            bool x = false;
            if (x)
            {
                throw new Exception("Inventory reservation failed.");
            }
        }
    }

}
