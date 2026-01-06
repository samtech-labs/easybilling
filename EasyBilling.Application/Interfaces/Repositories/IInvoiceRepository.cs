using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<Invoice?> GetByIdWithDetailsAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<Invoice?> GetLastInvoiceByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<Invoice?> GetLastInvoiceBySeriesAsync(Guid companyId, string series, CancellationToken cancellationToken = default);
        Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
        Task<InvoiceBlob?> GetInvoiceBlobByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task SaveInvoiceBlobAsync(InvoiceBlob blob, CancellationToken cancellationToken = default);
    }
}
