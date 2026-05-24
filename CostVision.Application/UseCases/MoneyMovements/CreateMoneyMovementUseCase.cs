using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class CreateMoneyMovementUseCase(IUnitOfWork unitOfWork) : ICreateMoneyMovementUseCase
    {
        public async Task<ServiceResult<MoneyMovementDto>> ExecuteAsync(CreateMoneyMovementRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.AccountId == Guid.Empty)
                return ServiceResult<MoneyMovementDto>.Fail(400, "Некорректный идентификатор счёта.");

            if (request.Amount == 0)
                return ServiceResult<MoneyMovementDto>.Fail(400, "Сумма операции не может быть равна нулю.");

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == request.AccountId && member.UserId == currentUserId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult<MoneyMovementDto>.Fail(404, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<MoneyMovementDto>.Fail(403, "Недостаточно прав для добавления операции в этот счёт.");

            Guid performedByUserId = request.PerformedByUserId.GetValueOrDefault(currentUserId);
            if (performedByUserId != currentUserId)
            {
                AccountMember? performedByMembership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                    member => member.AccountId == request.AccountId && member.UserId == performedByUserId,
                    asNoTracking: true,
                    ct: ct);

                if (performedByMembership == null)
                    return ServiceResult<MoneyMovementDto>.Fail(400, "Исполнитель операции должен быть участником счёта.");
            }

            MoneyMovementType movementType = request.Type ?? (request.Amount < 0 ? MoneyMovementType.Expense : MoneyMovementType.Income);
            DateTime occurredAt = request.OccurredAt == default ? DateTime.Now : request.OccurredAt;
            DateTime createdAtUtc = DateTime.UtcNow;

            MoneyMovement movement = new()
            {
                AccountId = request.AccountId,
                Amount = Math.Abs(request.Amount),
                Type = movementType,
                OccurredAt = occurredAt,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                CreatedByUserId = currentUserId,
                PerformedByUserId = performedByUserId,
                CreatedAtUtc = createdAtUtc,
                Source = MoneyMovementSource.Manual
            };

            unitOfWork.MoneyMovement.Create(movement);
            await unitOfWork.SaveChangesAsync(ct);

            MoneyMovement? created = await unitOfWork.MoneyMovement.GetItemByPredicateAsync(
                item => item.Id == movement.Id,
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Account)
                    .Include(item => item.PerformedByUser),
                ct: ct);

            if (created == null)
                return ServiceResult<MoneyMovementDto>.Fail(500, "Не удалось загрузить созданную операцию.");

            return ServiceResult<MoneyMovementDto>.Ok(created.MapDto());
        }
    }
}
