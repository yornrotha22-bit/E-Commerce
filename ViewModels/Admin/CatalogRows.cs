using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminBrandRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; }

    public int ProductCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AdminCategoryRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ParentName { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public int ProductCount { get; set; }

    public int ChildCount { get; set; }
}

public class AdminProductDetailsViewModel
{
    public Product Product { get; set; } = new();

    public IReadOnlyList<StockMovement> RecentMovements { get; set; } = Array.Empty<StockMovement>();
}
