namespace RothaShop.ViewModels;

public class CartViewModel
{
    public IReadOnlyList<CartItemViewModel> Items { get; set; } = Array.Empty<CartItemViewModel>();

    public decimal Subtotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal FreeShippingThreshold { get; set; }

    public decimal Total => Subtotal + ShippingFee;

    public int ItemCount => Items.Sum(i => i.Quantity);

    public bool IsEmpty => Items.Count == 0;

    public bool HasUnavailableItems => Items.Any(i => !i.IsAvailable);

    public bool QualifiesForFreeShipping => Subtotal > 0 && Subtotal >= FreeShippingThreshold;

    public decimal AmountToFreeShipping => QualifiesForFreeShipping
        ? 0m
        : Math.Max(0m, FreeShippingThreshold - Subtotal);

    public bool CanCheckout => !IsEmpty && !HasUnavailableItems;
}

public class CartItemViewModel
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int ProductVariantId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string VariantLabel { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public int StockQuantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public bool IsAvailable { get; set; }

    public string? UnavailableReason { get; set; }
}
