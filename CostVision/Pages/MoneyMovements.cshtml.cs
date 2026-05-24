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
        ICreateMoneyMovementUseCase createMoneyMovementUseCase,
        IMoveMoneyMovementToAccountUseCase moveMoneyMovementToAccountUseCase,
        IDeleteMoneyMovementUseCase deleteMoneyMovementUseCase,
        IUpdateMoneyMovementCommentUseCase updateMoneyMovementCommentUseCase,
        IGetBankStatementImportBanksUseCase getBankStatementImportBanksUseCase,
        IPreviewBankStatementImportUseCase previewBankStatementImportUseCase,
        IImportMoneyMovementsUseCase importMoneyMovementsUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getMoneyMovementAccountsUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<JsonResult> OnGetImportBanksAsync(CancellationToken ct)
        {
            List<BankStatementImportBankDto> banks = await getBankStatementImportBanksUseCase.ExecuteAsync(ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<BankStatementImportBankDto>>.Ok(banks));
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

        public async Task<JsonResult> OnPostMoveToAccountAsync([FromBody] MoveMoneyMovementToAccountRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await moveMoneyMovementToAccountUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostDeleteAsync([FromBody] DeleteMoneyMovementRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await deleteMoneyMovementUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateCommentAsync([FromBody] UpdateMoneyMovementCommentRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await updateMoneyMovementCommentUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostPreviewImportAsync([FromForm] string bankId, [FromForm] Guid accountId, [FromForm] IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return JsonResultMapper.ToJsonResult(ServiceResult<BankStatementImportPreviewDto>.Fail(400, "Выберите файл выписки."));

            await using Stream fileStream = file.OpenReadStream();
            ServiceResult<BankStatementImportPreviewDto> result = await previewBankStatementImportUseCase.ExecuteAsync(bankId, accountId, file.FileName, fileStream, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostImportAsync([FromBody] SaveBankStatementImportRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await importMoneyMovementsUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
