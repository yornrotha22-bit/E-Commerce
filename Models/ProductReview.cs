using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

public class ProductReview
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Optional link to the purchased line, used to verify a genuine purchase.
    /// </summary>
    public int? OrderItemId { get; set; }

    public OrderItem? OrderItem { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(150)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Reviews stay hidden until an administrator approves them.
    /// </summary>
    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
