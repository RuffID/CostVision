using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class AddReceiptModel(
        IGetUserAccountsForReceiptCreationUseCase getUserAccountsForReceiptCreationUseCase,
        ISaveReceiptsScannedUseCase saveReceiptsScannedUseCase,
        ISaveManualReceiptUseCase saveManualReceiptUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsForReceiptCreationUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<IActionResult> OnPostAsync([FromBody] QrScanRequest request, CancellationToken ct)
        {
            ServiceResult<AddReceiptScanResponse> result = await saveReceiptsScannedUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostManualAsync([FromBody] ReceiptManualCreateRequest input, CancellationToken ct)
        {
            ServiceResult<AddReceiptManualResponse> result = await saveManualReceiptUseCase.ExecuteAsync(input, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
