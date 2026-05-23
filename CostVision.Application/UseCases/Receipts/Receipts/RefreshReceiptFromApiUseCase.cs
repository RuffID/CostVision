using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshReceiptFromApiUseCase(IUnitOfWork unitOfWork, IReceiptAccessVerificationService accessVerification, IReceiptRefreshWorkflow receiptRefreshWorkflow) : IRefreshReceiptFromApiUseCase
    {
        public async Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<Receipt>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.Id == receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(receipt => receipt.Items)
                        .ThenInclude(item => item.Product)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
        }
    }
}
