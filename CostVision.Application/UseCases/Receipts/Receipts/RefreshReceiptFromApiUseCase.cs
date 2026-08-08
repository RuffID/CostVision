using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class RefreshReceiptFromApiUseCase(IUnitOfWork unitOfWork, IReceiptAccessVerificationService accessVerification, IReceiptRefreshWorkflow receiptRefreshWorkflow) : IRefreshReceiptFromApiUseCase
    {
        public async Task<ServiceResult<ReceiptDto>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.Id == receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.CreatedByUser)
                    .Include(receipt => receipt.Items)
                        .ThenInclude(item => item.Product)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.NotFound, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.Forbidden, "Нет доступа к этому чеку.");

            ServiceResult<Receipt> refreshResult = await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
            if (!refreshResult.Success)
                return refreshResult.PropagateFailure<ReceiptDto>();

            return ServiceResult<ReceiptDto>.Ok(refreshResult.Data.MapReceiptDto(currentUser.Id));
        }
    }
}
