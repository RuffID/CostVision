using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class MoneyMovementsModel(
        IGetMoneyMovementAccountsUseCase getMoneyMovementAccountsUseCase,
        IGetMoneyMovementListUseCase getMoneyMovementListUseCase,
        ICreateMoneyMovementUseCase createMoneyMovementUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getMoneyMovementAccountsUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<JsonResult> OnGetListAsync([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo, [FromQuery] Guid? accountId, CancellationToken ct)
        {
            Guid? normalizedAccountId = accountId == Guid.Empty ? null : accountId;
            ServiceResult<List<MoneyMovementDto>> result = await getMoneyMovementListUseCase.ExecuteAsync(CurrentUser.Id, dateFrom, dateTo, normalizedAccountId, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostCreateAsync([FromBody] CreateMoneyMovementRequest request, CancellationToken ct)
        {
            ServiceResult<MoneyMovementDto> result = await createMoneyMovementUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
