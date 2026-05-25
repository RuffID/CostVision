using CostVision.Web.Abstractions.Entity;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class ReceiptsModel(
        IGetUserAccountsUseCase getUserAccountsUseCase,
        IGetReceiptListUseCase getReceiptListUseCase,
        IGetReceiptWithItemsUseCase getReceiptWithItemsUseCase,
        IRefreshReceiptFromApiUseCase refreshReceiptFromApiUseCase,
        IDeleteReceiptUseCase deleteReceiptUseCase,
        IMoveReceiptToAccountUseCase moveReceiptToAccountUseCase,
        IRemoveReceiptFromAccountUseCase removeReceiptFromAccountUseCase,
        IGetLinkedReceiptMoneyMovementsUseCase getLinkedReceiptMoneyMovementsUseCase,
        IGetReceiptMoneyMovementCandidatesUseCase getReceiptMoneyMovementCandidatesUseCase,
        ILinkMoneyMovementReceiptUseCase linkMoneyMovementReceiptUseCase,
        IUnlinkMoneyMovementReceiptUseCase unlinkMoneyMovementReceiptUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsUseCase.ExecuteAsync(CurrentUser.Id, includeArchived: false, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<JsonResult> OnGetReceiptListAsync([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo, CancellationToken ct)
        {
            ServiceResult<List<Receipt>> result = await getReceiptListUseCase.ExecuteAsync(CurrentUser, dateFrom, dateTo, ct);

            if (!result.Success || result.Data == null)
                return JsonResultMapper.ToJsonResult(result);

            List<ReceiptDto> items = result.Data
                .GroupBy(receipt => receipt.GetIdentityKey())
                .Select(receiptGroup => receiptGroup.MapReceiptGroupDto(CurrentUser.Id))
                .OrderByDescending(r => r.DateTime)
                .ToList();

            foreach (ReceiptDto item in items)
            {
                ServiceResult<List<ReceiptMoneyMovementDto>> candidatesResult = await getReceiptMoneyMovementCandidatesUseCase.ExecuteAsync(
                    new GetReceiptMoneyMovementCandidatesRequest
                    {
                        ReceiptId = item.Id,
                        DateFrom = item.DateTime.Date,
                        DateTo = item.DateTime.Date,
                        UseAmountFilter = true,
                        AmountTolerance = 0,
                        ExcludeLinkedMoneyMovements = true
                    },
                    CurrentUser.Id,
                    ct);

                if (candidatesResult.Success && candidatesResult.Data != null)
                    item.AvailableMoneyMovementCount = candidatesResult.Data.Count;
            }

            return JsonResultMapper.ToJsonResult(ServiceResult<List<ReceiptDto>>.Ok(items));
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

        public async Task<JsonResult> OnPostDeleteReceiptAsync([FromBody] DeleteReceiptRequest request, CancellationToken ct)
        {
            if (request.ReceiptId == Guid.Empty)
                return JsonResultMapper.ToJsonResult(ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека."));

            ServiceResult<bool> serviceResult = await deleteReceiptUseCase.ExecuteAsync(request.ReceiptId, CurrentUser, ct);
            return JsonResultMapper.ToJsonResult(serviceResult);
        }

        public async Task<JsonResult> OnPostMoveReceiptToAccountAsync([FromBody] MoveReceiptToAccountRequest request, CancellationToken ct)
        {
            ServiceResult<bool> serviceResult = await moveReceiptToAccountUseCase.ExecuteAsync(request.SourceAccountId, request.TargetAccountId, request.ReceiptId, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(serviceResult);
        }

        public async Task<JsonResult> OnPostRemoveReceiptFromAccountAsync([FromBody] RemoveReceiptFromAccountRequest request, CancellationToken ct)
        {
            ServiceResult<bool> serviceResult = await removeReceiptFromAccountUseCase.ExecuteAsync(request.AccountId, request.ReceiptId, CurrentUser.Id, ct);
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
