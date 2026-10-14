using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Data;

using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class CreateMoneyMovementUseCase(IReceiptLinkUnitOfWork unitOfWork) : ICreateMoneyMovementUseCase
    {
        public async Task<ServiceResult<MoneyMovementDto>> ExecuteAsync(CreateMoneyMovementRequest request, Guid currentUserId, CancellationToken ct)
        {
            ServiceResult requestShape = RequestShapeValidation.Validate(request);
            if (!requestShape.Success)
            {
                return requestShape.PropagateFailure<MoneyMovementDto>();
            }

            Guid performedByUserId = request.PerformedByUserId.GetValueOrDefault(currentUserId);
            if (!MoneyMovement.TryCreateManual(
                    request.AccountId,
                    request.Amount,
                    request.Type,
                    request.OccurredAt,
                    request.Comment,
                    currentUserId,
                    performedByUserId,
                    DateTime.UtcNow,
                    out MoneyMovement? movement,
                    out string? error))
            {
                return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Validation, error!);
            }

            MoneyMovement newMovement = movement!;
            if (newMovement.Type == MoneyMovementType.Expense)
            {
                ServiceResult dateRange = RequestDateValidation.ValidateCalendarRange(newMovement.OccurredAt, newMovement.OccurredAt);
                if (!dateRange.Success)
                {
                    return dateRange.PropagateFailure<MoneyMovementDto>();
                }
            }

            ServiceResult<AccountMember> access = await MoneyMovementAccountAccessValidator.GetEditableAccountMemberAsync(
                unitOfWork.AccountMember, request.AccountId, currentUserId, ct, requireActive: true);
            if (!access.Success)
            {
                return access.PropagateFailure<MoneyMovementDto>();
            }

            if (performedByUserId != currentUserId)
            {
                AccountMember? performedByMembership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                    member => member.AccountId == request.AccountId && member.UserId == performedByUserId,
                    asNoTracking: true,
                    ct: ct);

                if (performedByMembership == null)
                {
                    return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Validation, "Исполнитель операции должен быть участником счёта.");
                }
            }

            MoneyMovementDto? createdDto = null;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    unitOfWork.MoneyMovement.Create(newMovement);
                    await unitOfWork.SaveChangesAsync(transactionCt);
                    await TryAutoLinkExactReceiptAsync(newMovement, currentUserId, transactionCt);

                    MoneyMovement? created = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                        item => item.Id == newMovement.Id,
                        asNoTracking: true,
                        include: query => query
                            .Include(item => item.Account)
                            .Include(item => item.PerformedByUser),
                        ct: transactionCt);

                    if (created == null)
                    {
                        throw new InvalidOperationException("Не удалось загрузить созданную операцию.");
                    }

                    createdDto = created.MapDto(currentUserId);
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException)
            {
                return ServiceResult<MoneyMovementDto>.Fail(ServiceErrorType.Conflict,
                    "Конкурентное создание операции отменено. Повторите запрос.");
            }

            return ServiceResult<MoneyMovementDto>.Ok(createdDto
                ?? throw new InvalidOperationException("Транзакция создания операции завершилась без результата."));
        }

        private async Task TryAutoLinkExactReceiptAsync(MoneyMovement movement, Guid currentUserId, CancellationToken ct)
        {
            if (movement.Type != MoneyMovementType.Expense)
            {
                return;
            }

            DateTime periodStart = movement.OccurredAt.Date;
            DateTime periodEnd = periodStart.AddDays(1);

            List<MoneyMovementReceipt> links = await ExactMoneyMovementReceiptMatcher.LoadAndBuildAsync(
                unitOfWork.MoneyMovement, unitOfWork.Receipt, currentUserId, periodStart, periodEnd, movement.AccountId, movement.Id, ct);
            if (links.Count == 0)
            {
                return;
            }

            unitOfWork.MoneyMovementReceipt.CreateRange(links);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
