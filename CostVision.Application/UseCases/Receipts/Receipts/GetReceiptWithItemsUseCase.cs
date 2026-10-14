using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class GetReceiptWithItemsUseCase(IReceiptRepository receiptRepository, IReceiptAccessVerificationService accessVerification) : IGetReceiptWithItemsUseCase
    {
        public async Task<ServiceResult<ReceiptDto>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
            {
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");
            }

            Receipt? receipt = await receiptRepository.GetItemByPredicateAsync(
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
                    .Include(receipt => receipt.MoneyMovementLinks)
                        .ThenInclude(link => link.MoneyMovement)
                            .ThenInclude(movement => movement!.Account)
                                .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
            {
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.NotFound, "Чек не найден.");
            }

            if (!FinancialDataAccess.CanReadReceipt(receipt, currentUser.Id) ||
                !accessVerification.UserHasAccessToReceipt(currentUser, receipt))
            {
                return ServiceResult<ReceiptDto>.Fail(ServiceErrorType.Forbidden, "Нет доступа к этому чеку.");
            }

            ReceiptDto dto = await ReceiptDocumentReportProjection.MapAsync(receiptRepository, receipt, currentUser.Id, ct);
            return ServiceResult<ReceiptDto>.Ok(dto);
        }
    }
}
