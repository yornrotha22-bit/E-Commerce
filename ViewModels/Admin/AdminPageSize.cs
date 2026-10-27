namespace RothaShop.ViewModels.Admin;

/// <summary>
/// Row counts the admin tables offer in their footer picker, and the guard that
/// keeps an arbitrary <c>?pageSize=</c> value from reaching the database query.
/// </summary>
public static class AdminPageSize
{
    public static readonly int[] Options = [5, 10, 15, 20, 50];

    public static int Normalize(int? requested, int fallback) =>
        requested is int value && Options.Contains(value) ? value : fallback;
}
