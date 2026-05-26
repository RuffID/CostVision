using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class GetReceiptListUseCase(IUnitOfWork unitOfWork) : IGetReceiptListUseCase
    {
        public async Task<ServiceResult<List<Receipt>>> ExecuteAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct)
        {
            if (dateTo.Date < dateFrom.Date)
                return ServiceResult<List<Receipt>>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

            DateTime periodStart = dateFrom.Date;
            DateTime periodEnd = dateTo.Date.AddDays(1);

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= periodStart &&
                           receipt.DateTime < periodEnd &&
                           (receipt.CreatedByUserId == currentUser.Id ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUser.Id) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUser.Id))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                        .ThenInclude(link => link.MoneyMovement)
                    .AsSplitQuery(),
                ct: ct);

            return ServiceResult<List<Receipt>>.Ok(receipts);
        }
    }
}
