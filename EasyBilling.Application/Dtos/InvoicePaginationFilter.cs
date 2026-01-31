namespace EasyBilling.Application.Dtos;

public sealed class InvoicePaginationFilter : PaginationFilter
{
    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public bool HasDateRange => DateFrom.HasValue || DateTo.HasValue;
}
