using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Application.Services;

public class InvoiceAnafSubmissionService(
    IInvoiceAnafSubmissionRepository invoiceAnafSubmissionRepository,
    ILogger<InvoiceAnafSubmissionService> logger
    ) : IInvoiceAnafSubmissionService
{
    private readonly IInvoiceAnafSubmissionRepository _invoiceAnafSubmissionRepository = invoiceAnafSubmissionRepository;
    private readonly ILogger<InvoiceAnafSubmissionService> _logger = logger;

    public async Task<InvoiceAnafSubmission?> AddAsync(
        InvoiceAnafSubmission invoiceAnafSubmission,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new ANAF submission for invoice {InvoiceId}, uploadIndex: {UploadIndex}",
            invoiceAnafSubmission.InvoiceId, invoiceAnafSubmission.UploadIndex);

        try
        {
            if (invoiceAnafSubmission == null)
            {
                _logger.LogWarning("Attempted to add null ANAF submission");
                return null;
            }

            _logger.LogDebug("ANAF submission details - InvoiceId: {InvoiceId}, UploadIndex: {UploadIndex}, Status: {Status}, SentXmlSize: {SentXmlSize} bytes",
                invoiceAnafSubmission.InvoiceId,
                invoiceAnafSubmission.UploadIndex,
                invoiceAnafSubmission.Status,
                invoiceAnafSubmission.SentXml?.Length ?? 0);

            await _invoiceAnafSubmissionRepository.AddAsync(invoiceAnafSubmission, cancellationToken);

            _logger.LogInformation("ANAF submission successfully created - SubmissionId: {SubmissionId}, InvoiceId: {InvoiceId}",
                invoiceAnafSubmission.Id, invoiceAnafSubmission.InvoiceId);

            return invoiceAnafSubmission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ANAF submission for invoice {InvoiceId}",
                invoiceAnafSubmission?.InvoiceId);
            throw;
        }
    }

    public async Task<InvoiceAnafSubmission?> GetByInvoiceIdAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving ANAF submission for invoice {InvoiceId}", invoiceId);

        try
        {
            var submission = await _invoiceAnafSubmissionRepository.GetByIdWithInvoiceAsync(invoiceId, cancellationToken);

            if (submission == null)
            {
                _logger.LogWarning("No ANAF submission found for invoice {InvoiceId}", invoiceId);
                return null;
            }

            _logger.LogInformation("ANAF submission retrieved for invoice {InvoiceId} - SubmissionId: {SubmissionId}, Status: {Status}, UploadIndex: {UploadIndex}",
                invoiceId, submission.Id, submission.Status, submission.UploadIndex);

            _logger.LogDebug("ANAF submission details - RetryCount: {RetryCount}, LastCheckedAt: {LastCheckedAt}, DownloadId: {DownloadId}",
                submission.RetryCount, submission.LastCheckedAt, submission.DownloadId ?? "none");

            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF submission for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }
}
