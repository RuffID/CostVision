using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Requests.Authorize;
using CostVision.Services.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CostVision.Controllers.Authorization
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController(IUnitOfWork unitOfWork, Hasher hasher) : Controller
    {
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Укажите логин и пароль." });

            User? user = await unitOfWork.User.GetItemByPredicate(u => u.Login == dto.Login, false, include: u => u.Include(u => u.Roles), ct);

            if (user == null || !hasher.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { error = "Неверный логин или пароль." });

            user.LastLoginAtUtc = DateTime.UtcNow;
            await unitOfWork.SaveAsync(ct);

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name)
            ];

            foreach (var role in user.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role.Name));


            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal claimsPrincipal = new(claimsIdentity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

            return Ok();
        }

        /*[HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok();
        }*/
    }
}
