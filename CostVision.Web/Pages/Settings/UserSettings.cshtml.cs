using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CostVision.Web.Abstractions.Entity;

namespace CostVision.Web.Pages.Settings
{
    [CookieAuthorize]
    [LoadUser]
    public class UserSettingsModel(
        IGetUserAccountsUseCase getUserAccountsUseCase,
        ICreateAccountUseCase createAccountUseCase,
        IUpdateAccountUseCase updateAccountUseCase,
        IGetAccountShareUsersUseCase getAccountShareUsersUseCase,
        IUpdateAccountMembersUseCase updateAccountMembersUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<IActionResult> OnGetAccountsAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsUseCase.ExecuteAsync(CurrentUser.Id, includeInactive, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<IActionResult> OnPostCreateAccountAsync([FromBody] CreateAccountRequest request, CancellationToken ct)
        {
            ServiceResult<Account> result = await createAccountUseCase.ExecuteAsync(CurrentUser.Id, request, ct);
            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            UserAccountViewModel dto = new()
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Description = result.Data.Description,
                ColorHex = result.Data.ColorHex,
                IsActive = !result.Data.IsArchived,
                CanManage = true,
                AccessRole = AccountAccessRole.Owner
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Ok(dto));
        }

        public async Task<IActionResult> OnPostUpdateAccountAsync([FromBody] UpdateAccountRequest request, CancellationToken ct)
        {
            ServiceResult<Account> result = await updateAccountUseCase.ExecuteAsync(CurrentUser.Id, request, ct);
            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            UserAccountViewModel dto = new()
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Description = result.Data.Description,
                ColorHex = result.Data.ColorHex,
                IsActive = !result.Data.IsArchived,
                CanManage = true,
                AccessRole = AccountAccessRole.Owner
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<UserAccountViewModel>.Ok(dto));
        }

        public async Task<IActionResult> OnGetShareCandidatesAsync([FromQuery] Guid accountId, CancellationToken ct)
        {
            return JsonResultMapper.ToJsonResult(await getAccountShareUsersUseCase.ExecuteAsync(accountId, CurrentUser.Id, ct));
        }

        public async Task<IActionResult> OnPostUpdateMembersAsync([FromBody] UpdateAccountMembersRequest request, CancellationToken ct)
        {
            return JsonResultMapper.ToJsonResult(await updateAccountMembersUseCase.ExecuteAsync(request.AccountId, CurrentUser.Id, request.Members, ct));
        }
    }
}
