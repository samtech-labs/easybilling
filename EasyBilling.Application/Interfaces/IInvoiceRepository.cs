using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(Guid invoiceId);
    }
}
