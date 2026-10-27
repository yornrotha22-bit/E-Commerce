namespace RothaShop.ViewModels;

public class WishlistRowViewModel
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal MinPrice { get; set; }

    public bool InStock { get; set; }

    public DateTime CreatedAt { get; set; }
}
