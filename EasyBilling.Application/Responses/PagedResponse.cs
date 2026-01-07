namespace EasyBilling.Application.Responses;

public class PagedResponse<T>
{
    public List<T> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } 
    public long TotalCount { get; set; }
}