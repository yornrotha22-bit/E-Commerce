using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Payment record for an order. A browser redirect never marks a payment as paid;
/// PaymentStatus.Paid is only set after server-side verification or an authorized staff update (COD).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>
    /// Provider identifier, for example "COD", "ABA", "Stripe", "Bakong".
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Provider side transaction reference. Unique per provider when present.
    /// </summary>
    [MaxLength(120)]
    public string? TransactionReference { get; set; }

    /// <summary>Dynamic KHQR payload generated for this payment attempt.</summary>
    [MaxLength(2000)]
    public string? QrPayload { get; set; }

    /// <summary>MD5 of the KHQR payload, used with Bakong's transaction check API.</summary>
    [MaxLength(32)]
    public string? QrMd5 { get; set; }

    public DateTime? QrExpiresAt { get; set; }

    [Range(0.01, 999_999_999)]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>
    /// Safe, non-sensitive provider message for support and audits.
    /// Never store card numbers, CVV values or credentials here.
    /// </summary>
    [MaxLength(2000)]
    public string? ProviderMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
