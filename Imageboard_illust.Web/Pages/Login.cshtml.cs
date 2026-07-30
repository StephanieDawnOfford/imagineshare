using Imageboard_illust.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Imageboard_illust.Web.Pages;

public class LoginModel : PageModel
{
    private readonly AppDatabase _db;

    public LoginModel(AppDatabase db)
    {
        _db = db;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return Page();
        }

        var user = _db.LoginUser(Email.Trim(), Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        return RedirectToPage("/Index");
    }
}
