using CostVision.Interfaces.Entity;
using CostVision.Interfaces.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Services.Receipts;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class AddReceiptModel(IReceiptService receiptService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnPostAsync([FromBody] QrScanRequest request, CancellationToken ct)
        {
            if (request.Results.Count == 0)
                return new JsonResult(new { success = false, errorMessage = "Нет данных для обработки." });

            ReceiptScanResultSummary summary = await receiptService.SaveReceiptsScannedAsync(request, CurrentUser.Id, ct);

            return new JsonResult(new
            {
                success = true,
                scannedCount = summary.ScannedCount,
                addedToDbCount = summary.AddedToDbCount,
                errorCount = summary.ErrorCount,
                results = summary.Results
            });
        }

        public async Task<IActionResult> OnPostManualAsync([FromBody] ReceiptManualCreateRequest input, CancellationToken ct)
        {
            ManualReceiptResult result = await receiptService.SaveReceiptManualAsync(input, CurrentUser.Id, ct);

            return new JsonResult(new { success = true, result });
        }
    }
}
