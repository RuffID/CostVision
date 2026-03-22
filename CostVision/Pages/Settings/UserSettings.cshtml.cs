using CostVision.Interfaces.Entity;
using CostVision.Interfaces.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Mappers;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages.Settings
{
    [CookieAuthorize]
    [LoadUser]
    public class UserSettingsModel(IAccountService accountService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnGetAccountsAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            List<UserAccountViewModel> accounts = await accountService.GetUserAccountsAsync(CurrentUser.Id, includeInactive, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<IActionResult> OnPostCreateAccountAsync([FromBody] CreateAccountRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3)
                return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Fail(400, "Название счёта обязательно. Минимум 3 символа."));

            ServiceResult<Account> result = await accountService.CreateAccountAsync(CurrentUser.Id, request, ct);
            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            UserAccountViewModel dto = new ()
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Description = result.Data.Description,
                ColorHex = result.Data.ColorHex,
                IsActive = !result.Data.IsArchived,
                CanManage = true
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Ok(dto));
        }


        public async Task<IActionResult> OnPostUpdateAccountAsync([FromBody] UpdateAccountRequest request, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Fail(400, "Некорректный идентификатор счёта."));

            if (string.IsNullOrWhiteSpace(request.Name))
                return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Fail(400, "Название счёта обязательно."));

            Account account = new()
            {
                Id = request.AccountId,
                Name = request.Name,
                Description = request.Description,
                ColorHex = request.ColorHex,
                IsArchived = !request.IsActive
            };

            ServiceResult<Account> result = await accountService.UpdateAccountAsync(CurrentUser.Id, account, ct);
            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            UserAccountViewModel dto = new ()
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Description = result.Data.Description,
                ColorHex = result.Data.ColorHex,
                IsActive = !result.Data.IsArchived,
                CanManage = true
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Ok(dto));
        }

        public async Task<IActionResult> OnGetShareCandidatesAsync([FromQuery] Guid accountId, CancellationToken ct)
        {
            return JsonResultMapper.ToJsonResult(await accountService.GetAccountShareUsersAsync(accountId, CurrentUser.Id, ct));
        }

        public async Task<IActionResult> OnPostUpdateMembersAsync([FromBody] UpdateAccountMembersRequest request, CancellationToken ct)
        {
            return JsonResultMapper.ToJsonResult(await accountService.UpdateAccountMembersAsync(request.AccountId, CurrentUser.Id, request.UserIds, ct));
        }
    }
}
