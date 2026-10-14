using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using System.Data;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class AutoLinkExactMoneyMovementReceiptsUseCase(IReceiptReconciliationUnitOfWork unitOfWork) : IAutoLinkExactMoneyMovementReceiptsUseCase
    {
        public async Task<ServiceResult<int>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct)
        {
            if (dateTo.Date < dateFrom.Date)
            {
                return ServiceResult<int>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");
            }

            DateTime periodStart = dateFrom.Date;
            DateTime periodEnd = dateTo.Date.AddDays(1);
            int count = 0;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    List<MoneyMovementReceipt> links = await ExactMoneyMovementReceiptMatcher.LoadAndBuildAsync(
                        unitOfWork.MoneyMovement, unitOfWork.Receipt, currentUserId, periodStart, periodEnd, accountId, null, transactionCt);
                    if (links.Count == 0)
                    {
                        return;
                    }

                    unitOfWork.MoneyMovementReceipt.CreateRange(links);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    count = links.Count;
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException)
            {
                return ServiceResult<int>.Fail(ServiceErrorType.Conflict, "Конкурентная сверка отменена. Повторите запрос.");
            }

            return ServiceResult<int>.Ok(count);
        }
    }
}
