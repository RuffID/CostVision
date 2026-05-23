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
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= dateFrom &&
                           receipt.DateTime <= dateTo &&
                           (receipt.CreatedByUserId == currentUser.Id ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUser.Id) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUser.Id))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            return ServiceResult<List<Receipt>>.Ok(receipts);
        }
    }
}
