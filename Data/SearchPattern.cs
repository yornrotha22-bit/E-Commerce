namespace RothaShop.Data;

/// <summary>
/// Helpers for free-text search fields. PostgreSQL <c>LIKE</c> is case sensitive,
/// so admin/storefront search boxes use <c>ILike</c> with a pattern built here.
/// </summary>
public static class SearchPattern
{
    /// <summary>Builds a "contains" pattern, dropping LIKE wildcards typed by the user.</summary>
    public static string Contains(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return "%";
        }

        var cleaned = term.Trim()
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        return $"%{cleaned}%";
    }

    /// <summary>Builds a "starts with" pattern, dropping LIKE wildcards typed by the user.</summary>
    public static string StartsWith(string? term)
    {
        return Contains(term).TrimEnd('%') + "%";
    }
}
