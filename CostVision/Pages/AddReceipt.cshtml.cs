using CostVision.Web.Abstractions.Entity;
using CostVision.Application.UseCases.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class AddReceiptModel(
        IGetUserAccountsForReceiptCreationUseCase getUserAccountsForReceiptCreationUseCase,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase,
        ISaveReceiptsScannedUseCase saveReceiptsScannedUseCase,
        ISaveManualReceiptUseCase saveManualReceiptUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsForReceiptCreationUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<IActionResult> OnPostAsync([FromBody] QrScanRequest request, CancellationToken ct)
        {
            if (request.Results.Count == 0)
                return JsonResultMapper.ToJsonResult(ServiceResult<AddReceiptScanResponse>.Fail(400, "Нет данных для обработки."));

            if (request.AccountId != Guid.Empty)
            {
                ServiceResult<bool> accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, CurrentUser.Id, ct);
                if (!accessResult.Success)
                    return JsonResultMapper.ToJsonResult(accessResult);
            }

            ReceiptScanResultSummary summary = await saveReceiptsScannedUseCase.ExecuteAsync(request, CurrentUser.Id, ct);

            AddReceiptScanResponse data = new()
            {
                ScannedCount = summary.ScannedCount,
                AddedToDbCount = summary.AddedToDbCount,
                ErrorCount = summary.ErrorCount,
                Results = summary.Results
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<AddReceiptScanResponse>.Ok(data));
        }

        public async Task<IActionResult> OnPostManualAsync([FromBody] ReceiptManualCreateRequest input, CancellationToken ct)
        {
            if (input.AccountId != Guid.Empty)
            {
                ServiceResult<bool> accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(input.AccountId, CurrentUser.Id, ct);
                if (!accessResult.Success)
                    return JsonResultMapper.ToJsonResult(accessResult);
            }

            ManualReceiptResult result = await saveManualReceiptUseCase.ExecuteAsync(input, CurrentUser.Id, ct);
            ReceiptDto? receiptDto = result.Receipt?.MapReceiptDto();

            AddReceiptManualResponse data = new()
            {
                IsCreated = result.IsCreated,
                Message = result.ErrorMessage,
                Receipt = receiptDto
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<AddReceiptManualResponse>.Ok(data));
        }
    }
}
