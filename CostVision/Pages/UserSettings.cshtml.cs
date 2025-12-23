using CostVision.Interfaces.Entity;
using CostVision.Interfaces.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class UserSettingsModel(IAccountService accountService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnGetAccountsAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            return new JsonResult(new { success = true, accounts = await accountService.GetUserAccountsAsync(CurrentUser.Id, includeInactive, ct) });
        }

        public async Task<IActionResult> OnPostCreateAccountAsync([FromBody] CreateAccountRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3)
                return new JsonResult(new { success = false, errorMessage = "Название счёта обязательно. Минимум 3 символа." });

            Account account = await accountService.CreateAccountAsync(CurrentUser.Id, request, ct);

            UserAccountViewModel dto = new ()
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                IsActive = !account.IsArchived,
                IsDefault = account.IsDefault
            };

            return new JsonResult(new { success = true, account = dto });
        }


        public async Task<IActionResult> OnPostUpdateAccountAsync([FromBody] UpdateAccountRequest request, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return new JsonResult(new { success = false, errorMessage = "Некорректный идентификатор счёта." });

            if (string.IsNullOrWhiteSpace(request.Name))
                return new JsonResult(new { success = false, errorMessage = "Название счёта обязательно." });

            Account account = new()
            {
                Id = request.AccountId,
                Name = request.Name,
                Description = request.Description,
                IsArchived = !request.IsActive,
                IsDefault= request.IsDefault
            };

            if (await accountService.UpdateAccountAsync(CurrentUser.Id, account, ct) == false)
                return new JsonResult(new { success = false, errorMessage = "Ошибка при редактировании счёта." });

            UserAccountViewModel dto = new ()
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                IsActive = !account.IsArchived,
                IsDefault = account.IsDefault
            };

            return new JsonResult(new { success = true, account = dto });
        }
    }
}