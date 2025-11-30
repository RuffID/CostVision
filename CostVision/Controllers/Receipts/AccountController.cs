using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Models.Authorization;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using Microsoft.AspNetCore.Mvc;

namespace CostVision.Controllers.Receipts
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(IUnitOfWork unitOfWork) : Controller
    {
        [HttpGet("by-user")]
        public async Task<IActionResult> GetAccountsByUser([FromQuery] Guid userId, CancellationToken ct)
        {
            if (userId.Equals(Guid.Empty))
                return BadRequest("Invalid user id.");

            List<Account>? accounts = await unitOfWork.Account.GetItemsByPredicate(a => a.CreatedByUserId == userId, asNoTracking: true, ct: ct);

            if (accounts == null)
                return NotFound();

            return Ok(accounts);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, [FromQuery] Guid userId, CancellationToken ct)
        {
            User? user = await unitOfWork.User.GetItemById(userId, ct: ct);

            if (user == null)
                return BadRequest($"User {userId} - not found.");

            Account? existingAccount = await unitOfWork.Account.GetItemByPredicate(a => a.CreatedByUserId == userId && a.Name == request.Name, ct: ct);

            if (existingAccount != null)
                return StatusCode(409, $"Account {request.Name} already exist.");

            Account account = new()
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            unitOfWork.Account.Create(account);
            user.Accounts.Add(account);

            return Ok();
        }
    }
}
