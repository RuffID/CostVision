using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class GetReceiptListPageUseCase(
        IGetReceiptListUseCase getReceiptListUseCase,
        IUnitOfWork unitOfWork) : IGetReceiptListPageUseCase
    {
        public async Task<ServiceResult<List<ReceiptDto>>> ExecuteAsync(User currentUser, GetReceiptListRequest request, CancellationToken ct)
        {
            ServiceResult<List<ReceiptDto>> receiptListResult = await getReceiptListUseCase.ExecuteAsync(currentUser, request.DateFrom, request.DateTo, ct);

            if (!receiptListResult.Success || receiptListResult.Data == null)
                return receiptListResult;

            List<ReceiptDto> receipts = receiptListResult.Data;
            if (receipts.Count == 0)
                return ServiceResult<List<ReceiptDto>>.Ok(receipts);

            DateTime periodStart = request.DateFrom.Date;
            DateTime periodEnd = request.DateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            !movement.ReceiptLinks.Any() &&
                            (movement.CreatedByUserId == currentUser.Id ||
                             movement.Account!.CreatedByUserId == currentUser.Id ||
                             movement.Account.Members.Any(member => member.UserId == currentUser.Id)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            foreach (ReceiptDto receipt in receipts)
            {
                receipt.AvailableMoneyMovementCount = CountAvailableMoneyMovements(receipt, movements, currentUser.Id);
            }

            return ServiceResult<List<ReceiptDto>>.Ok(receipts);
        }

        private static int CountAvailableMoneyMovements(ReceiptDto receipt, List<MoneyMovement> movements, Guid currentUserId)
        {
            DateTime receiptDate = receipt.DateTime.Date;
            List<Guid> receiptAccountIds = receipt.Accounts.Select(account => account.Id).ToList();

            return movements.Count(movement =>
                movement.OccurredAt.Date == receiptDate &&
                movement.Amount == receipt.TotalSum &&
                (receiptAccountIds.Contains(movement.AccountId) ||
                 (receiptAccountIds.Count == 0 && movement.CreatedByUserId == currentUserId)));
        }
    }
}
