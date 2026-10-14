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
            include: query => query.Include(receipt => receipt.Accounts), ct: ct);
        if (receipts.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Receipt> receiptsById = receipts.ToDictionary(receipt => receipt.Id);
        foreach (ReceiptDto item in items)
        {
            if (receiptsById.TryGetValue(item.Id, out Receipt? receipt))
            {
                List<int> counts = await unitOfWork.MoneyMovement.GetProjectedItemsAsync(
                    ReceiptMoneyMovementCandidateRules.ForInitialModal(receipt, currentUserId),
                    query => query.GroupBy(movement => 1).Select(group => group.Count()), ct);
                item.AvailableMoneyMovementCount = counts.SingleOrDefault();
            }
        }
    }
}
