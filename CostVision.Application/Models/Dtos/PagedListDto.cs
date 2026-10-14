namespace CostVision.Application.Models.Dtos;

public class PagedListDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    public static PagedListDto<T> Create(List<T> items, int count, int page, int pageSize)
    {
        int pages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        return new PagedListDto<T>
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = count, TotalPages = pages,
            HasPreviousPage = page > 1, HasNextPage = page < pages
        };
    }
}
