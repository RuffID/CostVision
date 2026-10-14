using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts;

internal static class ReceiptDocumentReportProjection
{
    public static async Task<ReceiptDto> MapAsync(IReceiptRepository receiptRepository, Receipt receipt, Guid currentUserId, CancellationToken ct)
    {
        List<Receipt> copies = await receiptRepository.GetItemsByPredicateAsync(
            ReceiptIdentityPredicates.ForDocument(receipt.GetIdentity()).WithReceiptReadAccess(item => item, currentUserId),
            asNoTracking: true,
            include: query => query.Include(item => item.MoneyMovementLinks)
                .ThenInclude(link => link.MoneyMovement).ThenInclude(movement => movement!.Account)
                .ThenInclude(account => account!.Members).AsSplitQuery(), ct: ct);

        if (copies.Count == 0)
        {
            throw new InvalidOperationException("Доступный набор копий документа не загружен для расчёта агрегатов.");
        }

        ReceiptDto dto = receipt.MapReceiptDto(currentUserId);
        ReceiptMoneyMovementAggregates.Apply(dto, copies, currentUserId);
        return dto;
    }
}
