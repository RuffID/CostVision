using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts;

internal static class ReceiptReportCandidateCounts
{
    public static async Task FillAsync(IUnitOfWork unitOfWork, List<ReceiptDto> items, Guid currentUserId, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        List<Guid> receiptIds = items.Select(item => item.Id).Distinct().ToList();
        Expression<Func<Receipt, bool>> receiptPredicate = receipt => receiptIds.Contains(receipt.Id);
        List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
            receiptPredicate.WithReceiptReadAccess(receipt => receipt, currentUserId),
            asNoTracking: true,
            include: query => query.Include(receipt => receipt.Accounts)
                .ThenInclude(link => link.Account).ThenInclude(account => account!.Members).AsSplitQuery(), ct: ct);
        if (receipts.Count == 0)
        {
            return;
        }

        DateTime dateFrom = receipts.Min(receipt => receipt.DateTime).Date.AddDays(-1);
        DateTime dateToExclusive = receipts.Max(receipt => receipt.DateTime).Date.AddDays(2);
        Expression<Func<MoneyMovement, bool>> movementPredicate = movement =>
            movement.OccurredAt >= dateFrom && movement.OccurredAt < dateToExclusive && !movement.ReceiptLinks.Any();
        List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
            movementPredicate.WithAccountReadAccess(movement => movement.Account, currentUserId),
            asNoTracking: true,
            include: query => query.Include(movement => movement.Account)
                .ThenInclude(account => account!.Members).Include(movement => movement.ReceiptLinks).AsSplitQuery(), ct: ct);

        Dictionary<Guid, Receipt> receiptsById = receipts.ToDictionary(receipt => receipt.Id);
        foreach (ReceiptDto item in items)
        {
            if (receiptsById.TryGetValue(item.Id, out Receipt? receipt))
            {
                // Этот уже доступный набор выполняется в памяти; EF-запрос выше сохраняет предикат доступа.
                item.AvailableMoneyMovementCount = movements.AsQueryable()
                    .Count(ReceiptMoneyMovementCandidateRules.ForInitialModal(receipt, currentUserId));
            }
        }
    }
}
