using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Key/value store settings editable by a SuperAdmin (store name, currency, shipping fee...).
/// </summary>
public class StoreSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Value { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
