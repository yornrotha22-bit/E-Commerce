using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminReviewRow
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string? Title { get; set; }

    public string Comment { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; }
}
