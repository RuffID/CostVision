using CostVision.Abstractions.Entity;
using CostVision.Abstractions.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Mappers;
using CostVision.Models.Dtos.Receipts;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class ReceiptsModel(IReceiptService receiptService, IAccountService accountService) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await accountService.GetUserAccountsAsync(CurrentUser.Id, false, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<JsonResult> OnGetReceiptListAsync([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo, CancellationToken ct)
        {
            ServiceResult<List<Receipt>> result = await receiptService.GetReceiptListAsync(CurrentUser, dateFrom, dateTo, ct);

            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            List<ReceiptDto> items = result.Data
                .OrderByDescending(r => r.DateTime)
                .Select(r => r.MapReceiptDto())
                .ToList();

            ServiceResult<List<ReceiptDto>> dtoResult = ServiceResult<List<ReceiptDto>>.Ok(items);

            return JsonResultMapper.ToJsonResult(dtoResult);
        }

        public async Task<JsonResult> OnPostOpenReceiptAsync([FromBody] OpenReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<Receipt> result = await receiptService.GetReceiptWithItemsAsync(request.ReceiptId, CurrentUser, ct);

            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            List<ReceiptItemDto> items = result.Data.Items
                .Select(i => new ReceiptItemDto
                {
                    Name = i.Product?.Name ?? "-",
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Sum = i.Sum
                })
                .ToList();

            ReceiptDto receiptDto = result.Data.MapReceiptDto();
            receiptDto.Items = items;

            ServiceResult<ReceiptDto> dtoResult = ServiceResult<ReceiptDto>.Ok(receiptDto);

            return JsonResultMapper.ToJsonResult(dtoResult);
        }

        public async Task<JsonResult> OnPostRefreshReceiptAsync([FromBody] RefreshReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<Receipt> result = await receiptService.RefreshReceiptFromApiAsync(request.ReceiptId, CurrentUser, ct);

            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            ReceiptDto receiptDto = result.Data.MapReceiptDto();

            ServiceResult<ReceiptDto> dtoResult = ServiceResult<ReceiptDto>.Ok(receiptDto);

            return JsonResultMapper.ToJsonResult(dtoResult);
        }

        public async Task<JsonResult> OnPostDeleteReceiptAsync([FromBody] DeleteReceiptRequest request, CancellationToken ct)
        {
            if (request.ReceiptId == Guid.Empty)
            {
                ServiceResult<bool> badIdResult = ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

                return JsonResultMapper.ToJsonResult(badIdResult);
            }

            ServiceResult<bool> serviceResult = await receiptService.DeleteReceiptAsync(request.ReceiptId, CurrentUser, ct);

            return JsonResultMapper.ToJsonResult(serviceResult);
        }

        public async Task<JsonResult> OnPostMoveReceiptToAccountAsync([FromBody] MoveReceiptToAccountRequest request, CancellationToken ct)
        {
            ServiceResult<bool> serviceResult = await accountService.MoveReceiptToAccountAsync(request.SourceAccountId, request.TargetAccountId, request.ReceiptId, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(serviceResult);
        }

        public async Task<JsonResult> OnPostRemoveReceiptFromAccountAsync([FromBody] RemoveReceiptFromAccountRequest request, CancellationToken ct)
        {
            ServiceResult<bool> serviceResult = await accountService.RemoveReceiptFromAccountAsync(request.AccountId, request.ReceiptId, CurrentUser.Id, ct);

            return JsonResultMapper.ToJsonResult(serviceResult);
        }
    }
}
