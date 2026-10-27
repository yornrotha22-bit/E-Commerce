using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RothaShop.Models;

namespace RothaShop.ViewComponents;

/// <summary>
/// Renders the signed-in user's avatar (or their initial) for the account menu.
/// </summary>
public class UserAvatarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAvatarViewComponent(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(UserClaimsPrincipal);

        return View("Default", user?.AvatarUrl);
    }
}
