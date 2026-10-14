using System.Linq.Expressions;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores;

internal static class StoreReadQueries
{
    public static IQueryable<Store> Representatives(IQueryable<Store> source, bool groupByName)
    {
        if (!groupByName)
        {
            return source;
        }

        return source.Where(store => !source.Any(other =>
            other.NormalizedName == store.NormalizedName && other.Id.CompareTo(store.Id) < 0));
    }

    public static IQueryable<StoreListItemDto> Project(IQueryable<Store> source,
        Expression<Func<Receipt, bool>> receiptPredicate, bool groupByName, bool useAdaptiveNames)
    {
        IQueryable<Receipt> purchases = source.SelectMany(store => store.Receipts).Where(receiptPredicate);
        return Representatives(source, groupByName).Select(store => new StoreListItemDto
        {
            Id = store.Id, Name = store.Name, Address = store.Address,
            AdaptiveName = store.AdaptiveName,
            DisplayName = useAdaptiveNames && store.AdaptiveName != null && store.AdaptiveName != "" ? store.AdaptiveName : store.Name,
            GroupKey = store.NormalizedName,
            ReceiptCount = purchases.Where(receipt => (groupByName && receipt.Store!.NormalizedName == store.NormalizedName) ||
                    (!groupByName && receipt.StoreId == store.Id)).Select(receipt => new { receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign, receipt.DateTime, receipt.TotalSum, receipt.OperationType }).Distinct().Count(),
            TotalSpent = purchases.Where(receipt => (groupByName && receipt.Store!.NormalizedName == store.NormalizedName) ||
                    (!groupByName && receipt.StoreId == store.Id)).Select(receipt => new { receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign, receipt.DateTime, receipt.TotalSum, receipt.OperationType }).Distinct()
                .Where(receipt => receipt.OperationType == ReceiptOperationType.Income ||
                    receipt.OperationType == ReceiptOperationType.RefundExpense).Sum(receipt => receipt.TotalSum),
            IncomeSum = purchases.Where(receipt => (groupByName && receipt.Store!.NormalizedName == store.NormalizedName) ||
                    (!groupByName && receipt.StoreId == store.Id)).Select(receipt => new { receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign, receipt.DateTime, receipt.TotalSum, receipt.OperationType }).Distinct()
                .Where(receipt => receipt.OperationType == ReceiptOperationType.Expense ||
                    receipt.OperationType == ReceiptOperationType.RefundIncome).Sum(receipt => receipt.TotalSum)
        });
    }

    public static IOrderedQueryable<StoreListItemDto> Sort(IQueryable<StoreListItemDto> source, string? sortBy, string? direction)
    {
        bool descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<StoreListItemDto> ordered;
        if (sortBy == "receiptCount")
        {
            if (descending)
            {
                ordered = source.OrderByDescending(item => item.ReceiptCount).ThenBy(item => item.Name).ThenBy(item => item.Address);
            }
            else
            {
                ordered = source.OrderBy(item => item.ReceiptCount).ThenBy(item => item.Name).ThenBy(item => item.Address);
            }
        }
        else if (sortBy == "totalSpent")
        {
            if (descending)
            {
                ordered = source.OrderByDescending(item => item.TotalSpent).ThenBy(item => item.Name).ThenBy(item => item.Address);
            }
            else
            {
                ordered = source.OrderBy(item => item.TotalSpent).ThenBy(item => item.Name).ThenBy(item => item.Address);
            }
        }
        else if (descending)
        {
            ordered = source.OrderByDescending(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount);
        }
        else
        {
            ordered = source.OrderBy(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount);
        }

        return ordered.ThenBy(item => item.Id);
    }
}
