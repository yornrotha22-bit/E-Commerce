using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Models;

namespace RothaShop.Controllers.Admin;

public class PendingOrdersViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _dbContext;

    public PendingOrdersViewComponent(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var pendingCount = await _dbContext.Orders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OrderStatus.Pending, HttpContext.RequestAborted);

        return View(pendingCount);
    }
}
