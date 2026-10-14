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

using CostVision.Web.Extensions;

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

        public IActionResult OnGetBrowserUser()
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            Response.Headers.CacheControl = "no-store";
            return JsonResultMapper.ToJsonResult(ServiceResult<Guid>.Ok(CurrentUser.Id));
        }

        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateBinding();
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<List<UserAccountViewModel>> result = await getUserAccountsForReceiptCreationUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostAsync([FromBody] QrScanRequest request, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(request);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<AddReceiptScanResponse> result = await saveReceiptsScannedUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<IActionResult> OnPostManualAsync([FromBody] ReceiptManualCreateRequest input, CancellationToken ct)
        {
            JsonResult? requestError = this.ValidateRequest(input);
            if (requestError != null)
            {
                return requestError;
            }

            ServiceResult<AddReceiptManualResponse> result = await saveManualReceiptUseCase.ExecuteAsync(input, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
