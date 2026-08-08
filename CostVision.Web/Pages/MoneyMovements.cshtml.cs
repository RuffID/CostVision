using CostVision.Web.Mappers;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Authorize.Attributes;
using CostVision.Web.Abstractions.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class MoneyMovementsModel(
        IMoneyMovementsPageUseCase moneyMovementsPageUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            ServiceResult<List<UserAccountViewModel>> result = await moneyMovementsPageUseCase.GetAccountsAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetImportBanksAsync(CancellationToken ct)
        {
            ServiceResult<List<BankStatementImportBankDto>> result = await moneyMovementsPageUseCase.GetImportBanksAsync(ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetListAsync([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo, [FromQuery] Guid? accountId, CancellationToken ct)
        {
            ServiceResult<List<MoneyMovementDto>> result = await moneyMovementsPageUseCase.GetListAsync(CurrentUser.Id, dateFrom, dateTo, accountId, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetLinkedReceiptsAsync([FromQuery] Guid moneyMovementId, CancellationToken ct)
        {
            ServiceResult<List<MoneyMovementReceiptDto>> result = await moneyMovementsPageUseCase.GetLinkedReceiptsAsync(moneyMovementId, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetReceiptCandidatesAsync([FromQuery] GetMoneyMovementReceiptCandidatesRequest request, CancellationToken ct)
        {
            ServiceResult<List<MoneyMovementReceiptDto>> result = await moneyMovementsPageUseCase.GetReceiptCandidatesAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostCreateAsync([FromBody] CreateMoneyMovementRequest request, CancellationToken ct)
        {
            ServiceResult<MoneyMovementDto> result = await moneyMovementsPageUseCase.CreateAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostMoveToAccountAsync([FromBody] MoveMoneyMovementToAccountRequest request, CancellationToken ct)
        {
            ServiceResult result = await moneyMovementsPageUseCase.MoveToAccountAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostDeleteAsync([FromBody] DeleteMoneyMovementRequest request, CancellationToken ct)
        {
            ServiceResult result = await moneyMovementsPageUseCase.DeleteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateCommentAsync([FromBody] UpdateMoneyMovementCommentRequest request, CancellationToken ct)
        {
            ServiceResult result = await moneyMovementsPageUseCase.UpdateCommentAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostLinkReceiptAsync([FromBody] LinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            ServiceResult result = await moneyMovementsPageUseCase.LinkReceiptAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUnlinkReceiptAsync([FromBody] UnlinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            ServiceResult result = await moneyMovementsPageUseCase.UnlinkReceiptAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostOpenReceiptAsync([FromBody] OpenReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<ReceiptDto> result = await moneyMovementsPageUseCase.OpenReceiptAsync(request, CurrentUser, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostPreviewImportAsync([FromForm] string bankId, [FromForm] Guid accountId, [FromForm] IFormFile file, CancellationToken ct)
        {
            await using Stream? fileStream = file?.OpenReadStream();
            ServiceResult<BankStatementImportPreviewDto> result = await moneyMovementsPageUseCase.PreviewImportAsync(
                new PreviewBankStatementImportPageRequest
                {
                    BankId = bankId,
                    AccountId = accountId,
                    FileName = file?.FileName ?? string.Empty,
                    FileStream = fileStream,
                    FileLength = file?.Length ?? 0
                },
                CurrentUser.Id,
                ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostImportAsync([FromBody] SaveBankStatementImportRequest request, CancellationToken ct)
        {
            ServiceResult<BankStatementImportResultDto> result = await moneyMovementsPageUseCase.ImportAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
