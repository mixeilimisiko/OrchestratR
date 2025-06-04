using OrchestratR.Core;

namespace DemoAPI.OrderSagaSync
{
    /// <summary>
    /// Context for a purely synchronous order saga.
    /// </summary>
    public class OrderSagaSyncContext : SagaContext
    {
        public Guid OrderId { get; set; }
        public bool InventoryReserved { get; set; }
        public bool PaymentProcessed { get; set; }
        public bool Shipped { get; set; }
    }
}
