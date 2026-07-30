using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Imageboard_illust.Web.Pages;

public class IndexModel : PageModel
{
    public bool IsAuthenticated { get; private set; }
    public string UserEmail { get; private set; } = string.Empty;

    public void OnGet()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId.HasValue)
        {
            IsAuthenticated = true;
            UserEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
        }
    }
}
