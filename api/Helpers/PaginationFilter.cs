using System.ComponentModel.DataAnnotations;

namespace SeatCharter.Api.Helpers;

public class PaginationFilter
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public string? QuerySearch { get; set; }
    public string? SortBy { get; set; }
    public string SortDir { get; set; } = "desc";
}

public class PaginationFilterWithDate : PaginationFilter
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class PagedResponse<T>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public string? QuerySearch { get; set; }
    public T? Data { get; set; }

    public PagedResponse() { }

    public PagedResponse(T data, int pageNumber, int pageSize, int totalRecords, string? querySearch = null)
    {
        Data = data;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
        TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        QuerySearch = querySearch;
    }
}
