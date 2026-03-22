using CostVision.Interfaces.Entity;
using CostVision.Interfaces.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Mappers;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Models.Services.Receipts;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class AddReceiptModel(IReceiptService receiptService, IAccountService accountService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await accountService.GetUserAccountsAsync(CurrentUser.Id, false, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<IActionResult> OnPostAsync([FromBody] QrScanRequest request, CancellationToken ct)
        {
            if (request.Results.Count == 0)
                return JsonResultMapper.ToJsonResult(ServiceResult<AddReceiptScanResponse>.Fail(400, "Нет данных для обработки."));

            ReceiptScanResultSummary summary = await receiptService.SaveReceiptsScannedAsync(request, CurrentUser.Id, ct);

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
            ManualReceiptResult result = await receiptService.SaveReceiptManualAsync(input, CurrentUser.Id, ct);

            AddReceiptManualResponse data = new()
            {
                IsCreated = result.IsCreated,
                Message = result.ErrorMessage,
                Receipt = result.Receipt
            };

            return JsonResultMapper.ToJsonResult(ServiceResult<AddReceiptManualResponse>.Ok(data));
        }
    }
}
