using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class GetReceiptListPageUseCase(
        IGetReceiptListUseCase getReceiptListUseCase,
        IUnitOfWork unitOfWork) : IGetReceiptListPageUseCase
    {
        public async Task<ServiceResult<ReceiptListDto>> ExecuteAsync(User currentUser, GetReceiptListRequest request, CancellationToken ct)
        {
            ServiceResult<List<ReceiptDto>> receiptListResult = await getReceiptListUseCase.ExecuteAsync(currentUser, request.DateFrom, request.DateTo, ct);

            if (!receiptListResult.Success || receiptListResult.Data == null)
                return ServiceResult<ReceiptListDto>.Fail(receiptListResult.Error!.StatusCode, receiptListResult.Error.Message);

            List<ReceiptDto> receipts = receiptListResult.Data;
            receipts = ApplyFilters(receipts, request);

            int pageSize = Math.Clamp(request.PageSize, 1, 100);
            int totalCount = receipts.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            int page = Math.Min(Math.Max(request.Page, 1), totalPages);
            decimal totalSum = receipts.Sum(receipt => receipt.TotalSum);

            if (receipts.Count == 0)
                return ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto { Page = page, PageSize = pageSize, TotalPages = totalPages });

            DateTime periodStart = request.DateFrom.Date;
            DateTime periodEnd = request.DateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            !movement.ReceiptLinks.Any() &&
                            (movement.CreatedByUserId == currentUser.Id ||
                             movement.Account!.CreatedByUserId == currentUser.Id ||
                             movement.Account.Members.Any(member => member.UserId == currentUser.Id)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptDto> pageReceipts = receipts.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            foreach (ReceiptDto receipt in pageReceipts)
            {
                receipt.AvailableMoneyMovementCount = CountAvailableMoneyMovements(receipt, movements, currentUser.Id);
            }

            return ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto
            {
                Items = pageReceipts,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages,
                TotalSum = totalSum
            });
        }

        private static List<ReceiptDto> ApplyFilters(List<ReceiptDto> receipts, GetReceiptListRequest request)
        {
            IEnumerable<ReceiptDto> query = receipts;

            if (request.AccountId.HasValue)
                query = query.Where(receipt => receipt.Accounts.Any(account => account.Id == request.AccountId.Value));

            string search = request.Search?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(search))
            {
                string normalizedSearch = search.ToLowerInvariant();
                query = query.Where(receipt => MatchesSearch(receipt, normalizedSearch, request.SearchMode));
            }

            query = request.OperationFilter switch
            {
                "withoutOperations" => query.Where(receipt => receipt.MoneyMovementCount == 0),
                "withOperations" => query.Where(receipt => receipt.MoneyMovementCount > 0),
                "amountMismatch" => query.Where(receipt => receipt.MoneyMovementCount > 0 && Math.Abs(receipt.TotalSum - receipt.MoneyMovementsTotalSum) >= 0.01m),
                _ => query
            };

            return query.ToList();
        }

        private static bool MatchesSearch(ReceiptDto receipt, string search, string? mode)
        {
            string store = receipt.RetailPlace.ToLowerInvariant();
            string accounts = string.Join(" ", receipt.Accounts.Select(account => account.Name)).ToLowerInvariant();
            return mode switch
            {
                "sum" => receipt.TotalSum.ToString().Contains(search),
                "shop" => store.Contains(search) || accounts.Contains(search),
                "fn" => receipt.FiscalDriveNumber.ToLowerInvariant().Contains(search),
                "fd" => receipt.FiscalDocumentNumber.ToLowerInvariant().Contains(search),
                "fp" => receipt.FiscalSign.ToLowerInvariant().Contains(search),
                _ => store.Contains(search) || accounts.Contains(search) || receipt.TotalSum.ToString().Contains(search) || receipt.FiscalDriveNumber.ToLowerInvariant().Contains(search) || receipt.FiscalDocumentNumber.ToLowerInvariant().Contains(search) || receipt.FiscalSign.ToLowerInvariant().Contains(search)
            };
        }

        private static int CountAvailableMoneyMovements(ReceiptDto receipt, List<MoneyMovement> movements, Guid currentUserId)
        {
            DateTime receiptDate = receipt.DateTime.Date;
            List<Guid> receiptAccountIds = receipt.Accounts.Select(account => account.Id).ToList();

            return movements.Count(movement =>
                movement.OccurredAt.Date == receiptDate &&
                movement.Amount == receipt.TotalSum &&
                (receiptAccountIds.Contains(movement.AccountId) ||
                 (receiptAccountIds.Count == 0 && movement.CreatedByUserId == currentUserId)));
        }
    }
}
