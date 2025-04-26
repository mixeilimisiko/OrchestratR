using OrchestratR.Core;

namespace DemoAPI.OrderSaga
{
    public class OrderSagaContext : SagaContext
    {
        public Guid OrderId { get; set; }
        public bool InventoryReserved { get; set; }
        public bool PaymentProcessed { get; set; }
        public bool Shipped { get; set; }
    }
}
