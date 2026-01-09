using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IInvoiceAnafSubmissionRepository
    {
        Task<InvoiceAnafSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<InvoiceAnafSubmission?> GetByIdWithInvoiceAsync(Guid id, CancellationToken ct = default);
        Task<InvoiceAnafSubmission?> GetLatestByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
        Task<InvoiceAnafSubmission?> GetSuccessfulByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
        Task AddAsync(InvoiceAnafSubmission submission, CancellationToken ct = default);
        Task UpdateAsync(InvoiceAnafSubmission submission, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}