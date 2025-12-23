using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Requests.Authorize;
using CostVision.Services.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Controllers.Authorization
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationController(IUnitOfWork unitOfWork, Hasher hasher) : Controller
    {
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        {
            User? exists = await unitOfWork.User.GetItemByPredicate(u => u.Login == request.Login, true, ct: ct);

            if (exists != null)
                return Conflict($"Login {request.Login} - already exist.");

            List<Role> roles = await unitOfWork.Role.GetItemsByPredicate(r => request.Roles.Contains(r.Id), ct: ct);

            if (roles.Count == 0)
                return BadRequest("No valid roles provided.");

            User user = new ()
            {
                Login = request.Login,
                Name = request.Name,
                PasswordHash = hasher.Hash(request.Password),
                IsActive = true,
                Roles = roles,
                CreatedAtUtc = DateTime.UtcNow
            };

            unitOfWork.User.Create(user);
            await unitOfWork.SaveChangesAsync(ct);

            return NoContent();
        }
    }
}
