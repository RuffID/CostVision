using CostVision.Web.Mappers;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Authorize.Attributes;
using CostVision.Web.Abstractions.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using CostVision.Web.Extensions;

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
        IImportMoneyMovementsUseCase importMoneyMovementsUseCase,
        IGetLinkedMoneyMovementReceiptsUseCase getLinkedMoneyMovementReceiptsUseCase,
        IGetMoneyMovementReceiptCandidatesUseCase getMoneyMovementReceiptCandidatesUseCase,
        ILinkMoneyMovementReceiptUseCase linkMoneyMovementReceiptUseCase,
        IUnlinkMoneyMovementReceiptUseCase unlinkMoneyMovementReceiptUseCase,
        IGetReceiptWithItemsUseCase getReceiptWithItemsUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            List<UserAccountViewModel> accounts = await getMoneyMovementAccountsUseCase.ExecuteAsync(CurrentUser.Id, ct);

            ServiceResult<List<UserAccountViewModel>> result = ServiceResult<List<UserAccountViewModel>>.Ok(accounts);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetImportBanksAsync(CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            List<BankStatementImportBankDto> banks = await getBankStatementImportBanksUseCase.ExecuteAsync(ct);

            ServiceResult<List<BankStatementImportBankDto>> result = ServiceResult<List<BankStatementImportBankDto>>.Ok(banks);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetListAsync([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo, [FromQuery] Guid? accountId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? receiptFilter = null)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<MoneyMovementListDto> result = await getMoneyMovementListUseCase.ExecuteAsync(CurrentUser.Id, dateFrom, dateTo, accountId, ct, page, pageSize, search, receiptFilter);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetLinkedReceiptsAsync([FromQuery] Guid moneyMovementId, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<List<MoneyMovementReceiptDto>> result = await getLinkedMoneyMovementReceiptsUseCase.ExecuteAsync(moneyMovementId, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetReceiptCandidatesAsync([FromQuery] GetMoneyMovementReceiptCandidatesRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<PagedListDto<MoneyMovementReceiptDto>> result = await getMoneyMovementReceiptCandidatesUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostCreateAsync([FromBody] CreateMoneyMovementRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<MoneyMovementDto> result = await createMoneyMovementUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostMoveToAccountAsync([FromBody] MoveMoneyMovementToAccountRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult result = await moveMoneyMovementToAccountUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostDeleteAsync([FromBody] DeleteMoneyMovementRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult result = await deleteMoneyMovementUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateCommentAsync([FromBody] UpdateMoneyMovementCommentRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult result = await updateMoneyMovementCommentUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostLinkReceiptAsync([FromBody] LinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult result = await linkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUnlinkReceiptAsync([FromBody] UnlinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult result = await unlinkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostOpenReceiptAsync([FromBody] OpenReceiptRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<ReceiptDto> result = await getReceiptWithItemsUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);

            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostPreviewImportAsync([FromForm] string bankId, [FromForm] Guid accountId, [FromForm] IFormFile file, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            await using Stream? fileStream = file?.OpenReadStream();

            ServiceResult<BankStatementImportPreviewDto> result = await previewBankStatementImportUseCase.ExecuteAsync(
                new PreviewBankStatementImportRequest
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
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<BankStatementImportResultDto> result = await importMoneyMovementsUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
