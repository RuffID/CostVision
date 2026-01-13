using CostVision.DataBase.Repositories;
using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Requests.Authorize;
using CostVision.Services.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CostVision.Pages
{
    [AllowAnonymous]
    public class LoginModel(IUnitOfWork unitOfWork, Hasher hasher) : PageModel
    {
        [BindProperty]
        public LoginRequest Input { get; set; } = new();

        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Укажи логин и пароль.";
                return Page();
            }

            User? user = await unitOfWork.User.GetItemByPredicate(u => u.Login == Input.Login, asNoTracking: false, include: u => u.Include(u => u.Roles), ct: ct);

            if (user == null || !hasher.Verify(Input.Password, user.PasswordHash))
            {
                ErrorMessage = "Неверный логин или пароль.";
                return Page();
            }

            user.LastLoginAtUtc = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name)
            ];

            foreach (var role in user.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role.Name));


            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal claimsPrincipal = new(claimsIdentity);

            AuthenticationProperties props = new()
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, props);

            return RedirectToPage("/Index");
        }
    }
}