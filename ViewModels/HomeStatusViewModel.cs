namespace RothaShop.ViewModels;

/// <summary>
/// Temporary status model for the Phase 1-2 landing page.
/// </summary>
public class HomeStatusViewModel
{
    public bool DatabaseOnline { get; set; }

    public string? ErrorMessage { get; set; }

    public int BrandCount { get; set; }

    public int CategoryCount { get; set; }

    public int ProductCount { get; set; }

    public int VariantCount { get; set; }

    public int CouponCount { get; set; }

    public int SuperAdminCount { get; set; }
}
