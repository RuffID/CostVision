using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class GetProductListUseCase(IUnitOfWork unitOfWork) : IGetProductListUseCase
    {
        private const int MIN_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;

        public async Task<ServiceResult<ProductListDto>> ExecuteAsync(GetProductListRequest request, CancellationToken ct)
        {
            int page = Math.Max(request.Page, MIN_PAGE);
            int pageSize = Math.Clamp(request.PageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);
            string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
            Expression<Func<Product, bool>> predicate = product =>
                search == null ||
                product.Name.Contains(search) ||
                (product.AdaptiveName != null && product.AdaptiveName.Contains(search));
            int totalCount = await unitOfWork.Product.CountByPredicateAsync(predicate, ct);
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;

            List<Product> products = await unitOfWork.Product.GetItemsByPredicateAsync(
                predicate,
                skip: skip,
                take: pageSize,
                asNoTracking: true,
                include: query => query.OrderBy(product => product.Name),
                ct: ct);

            List<ProductListItemDto> items = products
                .Select(product => new ProductListItemDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    AdaptiveName = product.AdaptiveName,
                    DisplayName = request.UseAdaptiveNames && !string.IsNullOrWhiteSpace(product.AdaptiveName)
                        ? product.AdaptiveName
                        : product.Name
                })
                .ToList();

            ProductListDto result = new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > MIN_PAGE,
                HasNextPage = page < totalPages
            };

            return ServiceResult<ProductListDto>.Ok(result);
        }
    }
}
