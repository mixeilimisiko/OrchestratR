namespace DemoAPI.OrderSagaSync
{
    /// <summary>
    /// Simulates a blocking inventory service.
    /// </summary>
    public interface IInventoryServiceSync
    {
        string Reserve(Guid orderId);
        void Release(string reservationId);
    }

    public class InventoryServiceSync : IInventoryServiceSync
    {
        public string Reserve(Guid orderId)
        {
            Console.WriteLine($"[InventoryServiceSync] Reserving inventory for order {orderId}...");
            // Simulate some work
            var reservationId = Guid.NewGuid().ToString();
            Console.WriteLine($"[InventoryServiceSync] Inventory reserved, reservationId={reservationId}");
            return reservationId;
        }

        public void Release(string reservationId)
        {
            Console.WriteLine($"[InventoryServiceSync] Releasing inventory reservation {reservationId}...");
            // Simulate some work
            Console.WriteLine($"[InventoryServiceSync] Inventory released for reservation {reservationId}");
        }
    }

    /// <summary>
    /// Simulates a blocking payment service.
    /// </summary>
    public interface IPaymentServiceSync
    {
        string Process(Guid orderId);
        void Refund(string paymentId);
    }

    public class PaymentServiceSync : IPaymentServiceSync
    {
        public string Process(Guid orderId)
        {
            Console.WriteLine($"[PaymentServiceSync] Processing payment for order {orderId}...");
            var paymentId = Guid.NewGuid().ToString();
            Console.WriteLine($"[PaymentServiceSync] Payment processed, paymentId={paymentId}");
            return paymentId;
        }

        public void Refund(string paymentId)
        {
            Console.WriteLine($"[PaymentServiceSync] Refunding payment {paymentId}...");
            Console.WriteLine($"[PaymentServiceSync] Payment refunded for {paymentId}");
        }
    }

    /// <summary>
    /// Simulates a blocking shipping service.
    /// </summary>
    public interface IShippingServiceSync
    {
        string Ship(Guid orderId);
        void Cancel(string shipmentId);
    }

    public class ShippingServiceSync : IShippingServiceSync
    {
        public string Ship(Guid orderId)
        {
            Console.WriteLine($"[ShippingServiceSync] Shipping order {orderId}...");
            var shipmentId = Guid.NewGuid().ToString();
            Console.WriteLine($"[ShippingServiceSync] Order shipped, shipmentId={shipmentId}");
            return shipmentId;
        }

        public void Cancel(string shipmentId)
        {
            Console.WriteLine($"[ShippingServiceSync] Cancelling shipment {shipmentId}...");
            Console.WriteLine($"[ShippingServiceSync] Shipment {shipmentId} cancelled");
        }
    }
}
