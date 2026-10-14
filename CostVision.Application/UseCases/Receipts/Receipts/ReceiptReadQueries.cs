using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts;

internal static class ReceiptReadQueries
{
    public static IQueryable<Receipt> Documents(IQueryable<Receipt> source)
    {
        return source.Where(receipt => !source.Any(other =>
            other.FiscalDriveNumber == receipt.FiscalDriveNumber &&
            other.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
            other.FiscalSign == receipt.FiscalSign && other.DateTime == receipt.DateTime &&
            other.TotalSum == receipt.TotalSum && other.OperationType == receipt.OperationType &&
            other.Id.CompareTo(receipt.Id) < 0));
    }

    public static IQueryable<ReadQueryTotals> Totals(IQueryable<Receipt> documents)
    {
        return documents.GroupBy(receipt => 1).Select(group => new ReadQueryTotals
        {
            Count = group.Count(),
            Expense = group.Sum(receipt => receipt.OperationType == ReceiptOperationType.Income ||
                receipt.OperationType == ReceiptOperationType.RefundExpense ? receipt.TotalSum : 0m),
            Income = group.Sum(receipt => receipt.OperationType == ReceiptOperationType.Expense ||
                receipt.OperationType == ReceiptOperationType.RefundIncome ? receipt.TotalSum : 0m),
            InvalidReceiptOperationCount = group.Count(receipt => receipt.OperationType != ReceiptOperationType.Income &&
                receipt.OperationType != ReceiptOperationType.RefundExpense && receipt.OperationType != ReceiptOperationType.Expense &&
                receipt.OperationType != ReceiptOperationType.RefundIncome)
        });
    }

    public static ReadQueryTotals GetTotals(List<ReadQueryTotals> summaries)
    {
        ReadQueryTotals totals = summaries.SingleOrDefault() ?? new ReadQueryTotals();
        if (totals.InvalidReceiptOperationCount != 0)
        {
            throw new InvalidOperationException("Неизвестный вид фискальной операции.");
        }

        return totals;
    }

    private static IQueryable<Receipt> PreferredDocuments(IQueryable<Receipt> source, Guid userId)
    {
        var ranked = source.Select(receipt => new
        {
            Copy = receipt,
            Editable = receipt.CreatedByUserId == userId && receipt.Accounts.Any(link => link.Account != null &&
                (link.Account.CreatedByUserId == userId || link.Account.Members.Any(member => member.UserId == userId &&
                    (member.Role == AccountAccessRole.Owner || member.Role == AccountAccessRole.Editor))))
        });
        return ranked.Where(item => !ranked.Any(other =>
            other.Copy.FiscalDriveNumber == item.Copy.FiscalDriveNumber &&
            other.Copy.FiscalDocumentNumber == item.Copy.FiscalDocumentNumber &&
            other.Copy.FiscalSign == item.Copy.FiscalSign && other.Copy.DateTime == item.Copy.DateTime &&
            other.Copy.TotalSum == item.Copy.TotalSum && other.Copy.OperationType == item.Copy.OperationType &&
            ((other.Editable && !item.Editable) ||
             (other.Editable == item.Editable && other.Copy.Id.ToString().CompareTo(item.Copy.Id.ToString()) < 0))))
            .Select(item => item.Copy);
    }

    public static IQueryable<Guid> PageIds(IQueryable<Receipt> documents, int skip, int take)
    {
        return documents.OrderByDescending(receipt => receipt.DateTime)
            .ThenBy(receipt => receipt.FiscalDriveNumber).ThenBy(receipt => receipt.FiscalDocumentNumber)
            .ThenBy(receipt => receipt.FiscalSign).ThenBy(receipt => receipt.TotalSum)
            .ThenBy(receipt => receipt.OperationType).ThenBy(receipt => receipt.Id)
            .Skip(skip).Take(take).Select(receipt => receipt.Id);
    }

    public static IQueryable<Receipt> CopiesForPage(IQueryable<Receipt> source, List<Guid> pageIds)
    {
        return source.Join(source.Where(receipt => pageIds.Contains(receipt.Id)),
            copy => new { copy.FiscalDriveNumber, copy.FiscalDocumentNumber, copy.FiscalSign, copy.DateTime, copy.TotalSum, copy.OperationType },
            document => new { document.FiscalDriveNumber, document.FiscalDocumentNumber, document.FiscalSign, document.DateTime, document.TotalSum, document.OperationType },
            (copy, document) => copy).OrderBy(copy => copy.Id);
    }

    public static IQueryable<Receipt> WithReportGraph(IQueryable<Receipt> query)
    {
        return query.Include(receipt => receipt.Store).Include(receipt => receipt.CreatedByUser)
            .Include(receipt => receipt.Accounts).ThenInclude(link => link.Account).ThenInclude(account => account!.Members)
            .Include(receipt => receipt.MoneyMovementLinks).ThenInclude(link => link.MoneyMovement)
                .ThenInclude(movement => movement!.Account).ThenInclude(account => account!.Members)
            .AsSplitQuery();
    }

    public static IQueryable<Receipt> FilterDocuments(IQueryable<Receipt> source, GetReceiptListRequest request, Guid userId)
    {
        IQueryable<Receipt> documents = PreferredDocuments(source, userId);
        var placements = source.SelectMany(receipt => receipt.Accounts.Where(link => link.Account != null &&
            (link.Account.CreatedByUserId == userId || link.Account.Members.Any(member => member.UserId == userId))),
            (receipt, link) => new
            {
                receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign, receipt.DateTime, receipt.TotalSum, receipt.OperationType,
                link.AccountId, Name = link.Account!.Name
            });
        if (request.AccountId.HasValue)
        {
            documents = documents.Where(receipt => placements.Any(link =>
                link.FiscalDriveNumber == receipt.FiscalDriveNumber && link.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                link.FiscalSign == receipt.FiscalSign && link.DateTime == receipt.DateTime &&
                link.TotalSum == receipt.TotalSum && link.OperationType == receipt.OperationType && link.AccountId == request.AccountId.Value));
        }

        var payments = source.SelectMany(receipt => receipt.MoneyMovementLinks.Where(link => link.MoneyMovement != null &&
            link.MoneyMovement.Account != null && (link.MoneyMovement.Account.CreatedByUserId == userId ||
                link.MoneyMovement.Account.Members.Any(member => member.UserId == userId))),
            (receipt, link) => new
            {
                receipt.FiscalDriveNumber, receipt.FiscalDocumentNumber, receipt.FiscalSign, receipt.DateTime, receipt.TotalSum, receipt.OperationType,
                link.MoneyMovementId, link.MoneyMovement!.Amount
            }).Distinct();
        if (request.OperationFilter == "withOperations" || request.OperationFilter == "withoutOperations")
        {
            bool requirePayment = request.OperationFilter == "withOperations";
            documents = documents.Where(receipt => payments.Any(link =>
                link.FiscalDriveNumber == receipt.FiscalDriveNumber && link.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                link.FiscalSign == receipt.FiscalSign && link.DateTime == receipt.DateTime &&
                link.TotalSum == receipt.TotalSum && link.OperationType == receipt.OperationType) == requirePayment);
        }
        else if (request.OperationFilter == "amountMismatch")
        {
            documents = documents.Where(receipt => payments.Any(link =>
                link.FiscalDriveNumber == receipt.FiscalDriveNumber && link.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                link.FiscalSign == receipt.FiscalSign && link.DateTime == receipt.DateTime &&
                link.TotalSum == receipt.TotalSum && link.OperationType == receipt.OperationType) &&
                Math.Abs(receipt.TotalSum - payments.Where(link =>
                    link.FiscalDriveNumber == receipt.FiscalDriveNumber && link.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                    link.FiscalSign == receipt.FiscalSign && link.DateTime == receipt.DateTime &&
                    link.TotalSum == receipt.TotalSum && link.OperationType == receipt.OperationType).Sum(link => link.Amount)) >= 0.01m);
        }

        string search = request.Search?.Trim().ToLowerInvariant() ?? "";
        if (search.Length > 0)
        {
            string mode = request.SearchMode ?? "";
            if (mode != "sum" && mode != "fn" && mode != "fd" && mode != "fp" && mode != "shop")
            {
                mode = "";
            }
            documents = documents.Where(receipt =>
                ((mode == "sum" || mode == "") && receipt.TotalSum.ToString().Contains(search)) ||
                ((mode == "fn" || mode == "") && receipt.FiscalDriveNumber.ToLower().Contains(search)) ||
                ((mode == "fd" || mode == "") && receipt.FiscalDocumentNumber.ToLower().Contains(search)) ||
                ((mode == "fp" || mode == "") && receipt.FiscalSign.ToLower().Contains(search)) ||
                ((mode == "shop" || mode == "") &&
                    ((receipt.Store != null ? receipt.Store.AdaptiveName ?? receipt.Store.Name : receipt.User ?? "").ToLower().Contains(search) ||
                     placements.Any(link => link.FiscalDriveNumber == receipt.FiscalDriveNumber &&
                        link.FiscalDocumentNumber == receipt.FiscalDocumentNumber && link.FiscalSign == receipt.FiscalSign &&
                        link.DateTime == receipt.DateTime && link.TotalSum == receipt.TotalSum &&
                        link.OperationType == receipt.OperationType && link.Name.ToLower().Contains(search)))));
        }

        return documents;
    }
}
