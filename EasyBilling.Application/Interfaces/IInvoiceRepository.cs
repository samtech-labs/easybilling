using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid invoiceId);
        Task<Invoice?> GetByIdWithDetailsAsync(Guid invoiceId);
        Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId);
        Task AddAsync(Invoice invoice);
    }
}
