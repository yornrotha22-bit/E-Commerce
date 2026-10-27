namespace RothaShop.Data.Configurations;

/// <summary>
/// Shared column type definitions so money and percentage precision stays consistent.
/// </summary>
internal static class ConfigurationDefaults
{
    /// <summary>
    /// Fixed precision for monetary values: decimal(18,2).
    /// </summary>
    public const string Money = "decimal(18,2)";

    /// <summary>
    /// Precision for percentage values: decimal(5,2) allows 0.01 .. 999.99.
    /// </summary>
    public const string Percentage = "decimal(5,2)";
}
