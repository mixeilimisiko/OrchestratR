using OrchestratR.Core;

namespace DemoAPI.OrderSaga
{
    public class ShipOrderStep : ISagaStep<OrderSagaContext>
    {
        public async Task<SagaStepStatus> ExecuteAsync(OrderSagaContext context, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Shipping order {context.OrderId}...");
            await Task.Delay(50);
            SimulateFailure();
            context.Shipped = true;
            Console.WriteLine("Order shipped.");
            return SagaStepStatus.Continue;
        }

        public async Task CompensateAsync(OrderSagaContext context, CancellationToken cancellationToken)
        {
            if (context.Shipped)
            {
                Console.WriteLine($"Cancelling shipment for order {context.OrderId}...");
                await Task.Delay(50);
                context.Shipped = false;
                Console.WriteLine("Shipment cancelled.");
            }
        }


        private void SimulateFailure()
        {
            // Simulate a failure in the inventory reservation step in debugging mode by manually changing value of x
            bool x = false;
            if (x)
            {
                throw new Exception("Shipment failed.");
            }
        }
    }
}
