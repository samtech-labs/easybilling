namespace EasyBilling.Application.Dtos;

public sealed class ClientPaginationFilter : PaginationFilter
{
    public Guid? CompanyId { get; set; }

    public string? City { get; set; }
}
