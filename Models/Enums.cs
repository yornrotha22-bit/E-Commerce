namespace RothaShop.Models;

/// <summary>
/// Lifecycle of an order from creation to delivery or cancellation.
/// Values are stored as integers in the database.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

/// <summary>
/// Payment state of an order. An order is only "Paid" after server-side verification.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    OnlinePayment = 1
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum StockMovementType
{
    Inbound = 0,
    Outbound = 1,
    Adjustment = 2,
    Return = 3
}
