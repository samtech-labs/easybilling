namespace EasyBilling.Application.Dtos;

public sealed class CompanyPaginationFilter : PaginationFilter
{
    public bool? IsVatPayer { get; set; }

    public bool? IsEFacturaActive { get; set; }

    public string? County { get; set; }
}