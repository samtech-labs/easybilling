using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdWithDetailsAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<Invoice>> GetInvoicesByCompanyIdPaginatedAsync(Guid companyId, InvoicePaginationFilter filter, CancellationToken cancellationToken = default);

    Task<List<Invoice>> GetAllCreditNotesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<Invoice>> GetCreditNotesByCompanyIdPaginatedAsync(Guid companyId,InvoicePaginationFilter filter,CancellationToken cancellationToken = default);

    Task<Invoice?> GetLastInvoiceByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Invoice?> GetLastInvoiceBySeriesAsync(Guid companyId, string series, CancellationToken cancellationToken = default);

    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
