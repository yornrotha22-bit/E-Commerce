using RothaShop.ViewModels;

namespace RothaShop.Tests;

public class PagedListTests
{
    [Fact]
    public void EmptyList_ReportsOnePageAndNoItems()
    {
        var page = new PagedList<int>(Array.Empty<int>(), page: 1, pageSize: 20, totalItems: 0);

        Assert.Equal(1, page.TotalPages);
        Assert.False(page.HasPrevious);
        Assert.False(page.HasNext);
        Assert.Equal(0, page.FirstItemNumber);
        Assert.Equal(0, page.LastItemNumber);
    }

    [Fact]
    public void MiddlePage_ComputesRangesAndFlags()
    {
        var page = new PagedList<int>(new[] { 21, 22 }, page: 2, pageSize: 20, totalItems: 45);

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal(21, page.FirstItemNumber);
        Assert.Equal(40, page.LastItemNumber);
    }

    [Fact]
    public void LastPage_ClampsTheLastItemNumber()
    {
        var page = new PagedList<int>(new[] { 41, 42, 43, 44, 45 }, page: 3, pageSize: 20, totalItems: 45);

        Assert.False(page.HasNext);
        Assert.Equal(41, page.FirstItemNumber);
        Assert.Equal(45, page.LastItemNumber);
    }

    [Fact]
    public void NonPositivePageSize_FallsBackToOnePage()
    {
        var page = new PagedList<int>(Array.Empty<int>(), page: 1, pageSize: 0, totalItems: 10);

        Assert.Equal(1, page.TotalPages);
    }
}
