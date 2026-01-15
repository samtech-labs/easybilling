using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services;

public class InvoiceAnafSubmissionService(
    IInvoiceAnafSubmissionRepository invoiceAnafSubmissionRepository
    ) : IInvoiceAnafSubmissionService
{
    private readonly IInvoiceAnafSubmissionRepository _invoiceAnafSubmissionRepository = invoiceAnafSubmissionRepository;

    public async Task<InvoiceAnafSubmission?> AddAsync(InvoiceAnafSubmission invoiceAnafSubmission, CancellationToken cancellationToken = default)
    {
        await _invoiceAnafSubmissionRepository.AddAsync(invoiceAnafSubmission, cancellationToken);
        return invoiceAnafSubmission;
    }

    public async Task<InvoiceAnafSubmission?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return await _invoiceAnafSubmissionRepository.GetByIdWithInvoiceAsync(invoiceId, cancellationToken);
    }
}
