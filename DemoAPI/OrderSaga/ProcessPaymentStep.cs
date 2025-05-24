using OrchestratR;
using OrchestratR.Core;

namespace DemoAPI.OrderSaga
{
    public class ProcessPaymentStep : ISagaStep<OrderSagaContext>
    {
        public async Task<SagaStepStatus> ExecuteAsync(OrderSagaContext context, CancellationToken cancellation)
        {
            Console.WriteLine($"Processing payment for order {context.OrderId}...");
            await Task.Delay(50);
            // Imagine we send a payment request to an external system here.
            // We will not mark PaymentProcessed yet, waiting for confirmation.
            Console.WriteLine("Payment request sent, awaiting confirmation...");
            return SagaStepStatus.Awaiting;
        }

        public async Task CompensateAsync(OrderSagaContext context, CancellationToken cancellation)
        {
            // If payment was processed and later something failed, compensate by refunding or canceling.
            if (context.PaymentProcessed)
            {
                Console.WriteLine($"Refunding payment for order {context.OrderId}...");
                await Task.Delay(50);
                context.PaymentProcessed = false;
                Console.WriteLine("Payment refunded.");
            }
        }
    }
}
