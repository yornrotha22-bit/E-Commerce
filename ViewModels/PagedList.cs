namespace RothaShop.ViewModels;

/// <summary>
/// Simple paging envelope used by admin list pages.
/// </summary>
public class PagedList<T>
{
    public PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalItems { get; }

    public int TotalPages => PageSize <= 0 || TotalItems <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public int FirstItemNumber => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalItems);
}
