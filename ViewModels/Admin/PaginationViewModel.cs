namespace RothaShop.ViewModels.Admin;

/// <summary>
/// Paging metadata shared by every admin table so the pagination footer looks
/// and behaves the same on all list pages.
/// </summary>
public class PaginationViewModel
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages { get; init; }

    public int TotalItems { get; init; }

    public int FirstItemNumber { get; init; }

    public int LastItemNumber { get; init; }

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PaginationViewModel From<T>(PagedList<T> list) => new()
    {
        Page = list.Page,
        PageSize = list.PageSize,
        TotalPages = list.TotalPages,
        TotalItems = list.TotalItems,
        FirstItemNumber = list.FirstItemNumber,
        LastItemNumber = list.LastItemNumber
    };

    /// <summary>
    /// Builds the same metadata for the admin list view models that carry
    /// paging fields directly instead of wrapping a <see cref="PagedList{T}"/>.
    /// </summary>
    public static PaginationViewModel Create(int page, int pageSize, int totalItems)
    {
        var totalPages = pageSize <= 0 || totalItems <= 0
            ? 1
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PaginationViewModel
        {
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalItems = totalItems,
            FirstItemNumber = totalItems == 0 ? 0 : ((page - 1) * pageSize) + 1,
            LastItemNumber = Math.Min(page * pageSize, totalItems)
        };
    }
}
