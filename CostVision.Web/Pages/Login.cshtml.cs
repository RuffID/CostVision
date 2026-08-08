using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using CostVision.Application.UseCases.Authorize.Authentication;

namespace CostVision.Web.Pages
{
    [AllowAnonymous]
    public class LoginModel(IAuthenticateUserUseCase authenticateUserUseCase) : PageModel
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

            ServiceResult<AuthenticatedUserDto> authenticationResult = await authenticateUserUseCase.ExecuteAsync(Input, ct);
            if (!authenticationResult.Success)
            {
                ErrorMessage = authenticationResult.Error.Message;
                return Page();
            }

            AuthenticatedUserDto user = authenticationResult.Data;

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name)
            ];

            foreach (RoleType role in user.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal claimsPrincipal = new(claimsIdentity);

            AuthenticationProperties props = new() { IsPersistent = true };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, props);

            return RedirectToPage("/Index");
        }
    }
}
