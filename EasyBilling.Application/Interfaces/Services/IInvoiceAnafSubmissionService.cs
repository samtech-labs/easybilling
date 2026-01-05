using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IInvoiceAnafSubmissionService
    {
        Task<InvoiceAnafSubmission?> AddAsync(InvoiceAnafSubmission invoiceAnafSubmission, CancellationToken cancellationToken = default);
        Task<InvoiceAnafSubmission?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    }
}
