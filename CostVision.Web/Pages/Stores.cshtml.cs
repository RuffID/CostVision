using CostVision.Web.Mappers;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
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
        IGetUserAccountsUseCase getUserAccountsUseCase,
        IGetReceiptWithItemsUseCase getReceiptWithItemsUseCase,
        IRefreshReceiptFromApiUseCase refreshReceiptFromApiUseCase,
        IMoveReceiptToAccountUseCase moveReceiptToAccountUseCase,
        IRemoveReceiptFromAccountUseCase removeReceiptFromAccountUseCase,
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
            ServiceResult result = await updateStoreAdaptiveNameUseCase.ExecuteAsync(request, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            ServiceResult<List<UserAccountViewModel>> result = await getUserAccountsUseCase.ExecuteAsync(CurrentUser.Id, includeArchived: false, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostOpenReceiptAsync([FromBody] OpenReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<ReceiptDto> result = await getReceiptWithItemsUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostRefreshReceiptAsync([FromBody] RefreshReceiptRequest request, CancellationToken ct)
        {
            ServiceResult<ReceiptDto> result = await refreshReceiptFromApiUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostMoveReceiptToAccountAsync([FromBody] MoveReceiptToAccountRequest request, CancellationToken ct)
        {
            ServiceResult serviceResult = await moveReceiptToAccountUseCase.ExecuteAsync(request.SourceAccountId, request.TargetAccountId, request.ReceiptId, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(serviceResult);
        }

        public async Task<JsonResult> OnPostRemoveReceiptFromAccountAsync([FromBody] RemoveReceiptFromAccountRequest request, CancellationToken ct)
        {
            ServiceResult serviceResult = await removeReceiptFromAccountUseCase.ExecuteAsync(request.AccountId, request.ReceiptId, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(serviceResult);
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
            ServiceResult result = await linkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUnlinkMoneyMovementAsync([FromBody] UnlinkMoneyMovementReceiptRequest request, CancellationToken ct)
        {
            ServiceResult result = await unlinkMoneyMovementReceiptUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
