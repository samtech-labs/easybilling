namespace EasyBilling.Application.Dtos;

public abstract class PaginationFilter : PaginationRequest
{
    public string? SearchTerm { get; set; }

    public string SortOrder { get; set; } = "asc";

    public string? SortBy { get; set; }

    public bool HasSearchTerm => !string.IsNullOrWhiteSpace(SearchTerm);

    public bool HasSortBy => !string.IsNullOrWhiteSpace(SortBy);

    public bool IsValidSortOrder =>
        SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
        SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
}
