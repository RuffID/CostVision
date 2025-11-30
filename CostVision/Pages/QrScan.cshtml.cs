using CostVision.Interfaces.Entity;
using CostVision.Interfaces.Service.Receipt;
using CostVision.Models.Authorization;
using CostVision.Models.Services.Receipts;
using CostVision.Services.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class QrScanModel(IReceiptService receiptService) : PageModel, IHasCurrentUser
    {
        [BindProperty]
        public List<QrScanResult> Results { get; set; } = new();

        public User CurrentUser { get; set; } = new();

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            ReceiptScanResultSummary summary = await receiptService.SaveScannedReceiptsAsync(Results, CurrentUser.Id, ct);

            return new JsonResult(new
            {
                success = true,
                scannedCount = summary.ScannedCount,
                addedToDbCount = summary.AddedToDbCount,
                errorCount = summary.ErrorCount,
                results = summary.Results
            });
        }

        public async Task<IActionResult> OnPostManualAsync([FromBody] ManualReceiptInput input, CancellationToken ct)
        {
            ManualReceiptResult result = await receiptService.SaveManualReceiptAsync(input, CurrentUser.Id, ct);

            return new JsonResult(new { success = true, result });
        }
    }
}
