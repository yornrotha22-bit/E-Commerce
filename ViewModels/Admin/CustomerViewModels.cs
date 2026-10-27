using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminCustomerRow
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public int OrderCount { get; set; }

    public decimal PaidTotal { get; set; }

    public DateTime? LastOrderAt { get; set; }
}

public class AdminCustomerListViewModel
{
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalItems { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<AdminCustomerRow> Items { get; set; } = Array.Empty<AdminCustomerRow>();
}

public class AdminCustomerDetailsViewModel
{
    public ApplicationUser Customer { get; set; } = new();

    public IReadOnlyList<Address> Addresses { get; set; } = Array.Empty<Address>();

    public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();
}
