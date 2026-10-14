using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts;

public class GetReceiptListPageUseCase(IReceiptRepository receiptRepository, IMoneyMovementRepository moneyMovementRepository) : IGetReceiptListPageUseCase
{
    public async Task<ServiceResult<ReceiptListDto>> ExecuteAsync(User currentUser, GetReceiptListRequest request, CancellationToken ct)
    {
        ServiceResult requestShape = RequestShapeValidation.Validate(request);
        if (!requestShape.Success)
        {
            return requestShape.PropagateFailure<ReceiptListDto>();
        }

        ServiceResult dateRange = RequestDateValidation.ValidateCalendarRange(request.DateFrom, request.DateTo);
        if (!dateRange.Success)
        {
            return dateRange.PropagateFailure<ReceiptListDto>();
        }


        DateTime start = request.DateFrom.Date;
        DateTime end = request.DateTo.Date.AddDays(1);
        Expression<Func<Receipt, bool>> predicate = receipt => receipt.DateTime >= start && receipt.DateTime < end;
        predicate = predicate.WithReceiptReadAccess(receipt => receipt, currentUser.Id);
        List<ReadQueryTotals> summaries = await receiptRepository.GetProjectedItemsAsync(predicate,
            query => ReceiptReadQueries.Totals(ReceiptReadQueries.FilterDocuments(query, request, currentUser.Id)), ct);
        ReadQueryTotals totals = ReceiptReadQueries.GetTotals(summaries);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totals.Count / (double)pageSize));
        int page = Math.Clamp(request.Page, 1, totalPages);
        List<Guid> ids = await receiptRepository.GetProjectedItemsAsync(predicate,
            query => ReceiptReadQueries.PageIds(ReceiptReadQueries.FilterDocuments(query, request, currentUser.Id),
                (page - 1) * pageSize, pageSize), ct);
        List<ReceiptDto> items = [];
        if (ids.Count > 0)
        {
            List<Receipt> copies = await receiptRepository.GetProjectedItemsAsync(predicate,
                query => ReceiptReadQueries.WithReportGraph(ReceiptReadQueries.CopiesForPage(query, ids)), ct);
            Dictionary<ReceiptIdentity, int> positions = copies.Where(copy => ids.Contains(copy.Id))
                .ToDictionary(copy => copy.GetIdentity(), copy => ids.IndexOf(copy.Id));
            items = copies.GroupBy(copy => copy.GetIdentity()).OrderBy(group => positions[group.Key])
                .Select(group => group.MapReceiptGroupDto(currentUser.Id)).ToList();
            ServiceResult candidateCounts = await ReceiptReportCandidateCounts.FillAsync(receiptRepository, moneyMovementRepository, items, currentUser.Id, ct);
            if (!candidateCounts.Success)
            {
                return candidateCounts.PropagateFailure<ReceiptListDto>();
            }
        }

        return ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = totals.Count, TotalPages = totalPages,
            HasPreviousPage = page > 1, HasNextPage = page < totalPages, TotalSum = totals.Expense, IncomeSum = totals.Income
        });
    }
}
