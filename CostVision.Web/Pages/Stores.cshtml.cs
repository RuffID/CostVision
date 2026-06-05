using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class StoresModel(
        IGetStoreListUseCase getStoreListUseCase,
        IGetStoreReceiptListUseCase getStoreReceiptListUseCase,
        IUpdateStoreAdaptiveNameUseCase updateStoreAdaptiveNameUseCase,
        IGetReceiptWithItemsUseCase getReceiptWithItemsUseCase,
        IRefreshReceiptFromApiUseCase refreshReceiptFromApiUseCase,
        IGetLinkedReceiptMoneyMovementsUseCase getLinkedReceiptMoneyMovementsUseCase,
        IGetReceiptMoneyMovementCandidatesUseCase getReceiptMoneyMovementCandidatesUseCase,
        ILinkMoneyMovementReceiptUseCase linkMoneyMovementReceiptUseCase,
        IUnlinkMoneyMovementReceiptUseCase unlinkMoneyMovementReceiptUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetListAsync([FromQuery] GetStoreListRequest request, CancellationToken ct)
        {
            ServiceResult<StoreListDto> result = await getStoreListUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetReceiptsAsync([FromQuery] GetStoreReceiptListRequest request, CancellationToken ct)
        {
            ServiceResult<StoreReceiptListDto> result = await getStoreReceiptListUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateAdaptiveNameAsync([FromBody] UpdateStoreAdaptiveNameRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await updateStoreAdaptiveNameUseCase.ExecuteAsync(request, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostOpenReceiptAsync([FromBody] OpenReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<Receipt> result = await getReceiptWithItemsUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);

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

            ReceiptDto receiptDto = result.Data.MapReceiptDto(CurrentUser.Id);
            receiptDto.Items = items;

            return JsonResultMapper.ToJsonResult(ServiceResult<ReceiptDto>.Ok(receiptDto));
        }

        public async Task<JsonResult> OnPostRefreshReceiptAsync([FromBody] RefreshReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<Receipt> result = await refreshReceiptFromApiUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);

            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            ReceiptDto receiptDto = result.Data.MapReceiptDto(CurrentUser.Id);
            return JsonResultMapper.ToJsonResult(ServiceResult<ReceiptDto>.Ok(receiptDto));
        }

        public async Task<JsonResult> OnGetLinkedMoneyMovementsAsync([FromQuery] Guid receiptId, CancellationToken ct)
        {
            ServiceResult<List<ReceiptMoneyMovementDto>> result = await getLinkedReceiptMoneyMovementsUseCase.ExecuteAsync(receiptId, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetMoneyMovementCandidatesAsync([FromQuery] GetReceiptMoneyMovementCandidatesRequest request, CancellationToken ct)
        {
            ServiceResult<List<ReceiptMoneyMovementDto>> result = await getReceiptMoneyMovementCandidatesUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostLinkMoneyMovementAsync([FromBody] LinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await linkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUnlinkMoneyMovementAsync([FromBody] UnlinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await unlinkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
