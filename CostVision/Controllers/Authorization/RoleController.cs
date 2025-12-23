using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Requests.Authorize;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Controllers.Authorization
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController(IUnitOfWork unitOfWork) : Controller
    {
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken ct)
        {
            Role? exists = await unitOfWork.Role.GetItemByPredicate(r => r.Name.ToLower() == request.Name.ToLower(), true, ct: ct);

            if (exists != null)
                return Conflict(new { error = $"Role {request.Name} - already exist." });

            Role role = new()
            {
                Name = request.Name.Trim()
            };

            unitOfWork.Role.Create(role);
            await unitOfWork.SaveChangesAsync(ct);

            return NoContent();
        }
    }
}
