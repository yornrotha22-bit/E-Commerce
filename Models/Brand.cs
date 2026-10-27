using System.ComponentModel.DataAnnotations;
namespace RothaShop.Models;
public class Brand
{
    public int Id { get; set; }
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// URL friendly identifier, unique across brands.
    /// </summary>
    [Required]
    [MaxLength(160)]
    public string Slug { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Description { get; set; }
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
