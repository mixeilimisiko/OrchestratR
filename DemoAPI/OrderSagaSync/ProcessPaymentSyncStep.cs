using OrchestratR.Core;

namespace DemoAPI.OrderSagaSync
{
    public class ProcessPaymentSyncStep : ISagaStep<OrderSagaSyncContext>
    {
        private readonly IPaymentServiceSync _paymentService;

        public ProcessPaymentSyncStep(IPaymentServiceSync paymentService)
        {
            _paymentService = paymentService;
        }

        public Task<SagaStepStatus> ExecuteAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[ProcessPaymentSyncStep] Executing for order {context.OrderId}...");
            // Simulate a failure trigger (set x = true to throw)
            bool x = false;
            if (x)
            {
                throw new Exception("Payment processing failed (sync)!");
            }

            // Perform a synchronous payment
            var paymentId = _paymentService.Process(context.OrderId);
            context.PaymentProcessed = true;
            Console.WriteLine($"[ProcessPaymentSyncStep] PaymentProcessed=true");
            return Task.FromResult(SagaStepStatus.Continue);
        }

        public Task CompensateAsync(OrderSagaSyncContext context, CancellationToken cancellationToken = default)
        {
            if (context.PaymentProcessed)
            {
                Console.WriteLine($"[ProcessPaymentSyncStep] Compensating (refunding payment) for order {context.OrderId}...");
                _paymentService.Refund(Guid.NewGuid().ToString());
                context.PaymentProcessed = false;
                Console.WriteLine($"[ProcessPaymentSyncStep] PaymentProcessed=false");
            }
            return Task.CompletedTask;
        }
    }
}
