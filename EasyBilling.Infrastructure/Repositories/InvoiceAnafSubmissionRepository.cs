using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Repositories;

public class InvoiceAnafSubmissionRepository : IInvoiceAnafSubmissionRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<InvoiceAnafSubmissionRepository> _logger;

    public InvoiceAnafSubmissionRepository(AppDbContext dbContext, ILogger<InvoiceAnafSubmissionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<InvoiceAnafSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving ANAF submission by ID: {SubmissionId}", id);

        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve ANAF submission with empty SubmissionId");
                return null;
            }

            var submission = await _dbContext.InvoiceAnafSubmissions
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (submission == null)
            {
                _logger.LogWarning("ANAF submission {SubmissionId} not found", id);
                return null;
            }

            _logger.LogDebug("ANAF submission retrieved: {SubmissionId}, Status: {Status}", id, submission.Status);
            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF submission {SubmissionId}", id);
            throw;
        }
    }

    public async Task<InvoiceAnafSubmission?> GetByIdWithInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving ANAF submission with invoice by ID: {SubmissionId}", id);

        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve ANAF submission with empty SubmissionId");
                return null;
            }

            var submission = await _dbContext.InvoiceAnafSubmissions
                .Include(s => s.Invoice)
                    .ThenInclude(i => i.Company)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (submission == null)
            {
                _logger.LogWarning("ANAF submission {SubmissionId} with invoice not found", id);
                return null;
            }

            _logger.LogDebug("ANAF submission with invoice retrieved: {SubmissionId}, InvoiceId: {InvoiceId}, Status: {Status}",
                id, submission.InvoiceId, submission.Status);
            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF submission {SubmissionId} with invoice", id);
            throw;
        }
    }

    public async Task<InvoiceAnafSubmission?> GetLatestByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving latest ANAF submission for invoice: {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve ANAF submission with empty InvoiceId");
                return null;
            }

            var submission = await _dbContext.InvoiceAnafSubmissions
                .Where(s => s.InvoiceId == invoiceId)
                .OrderByDescending(s => s.UploadedAt)
                .FirstOrDefaultAsync(ct);

            if (submission == null)
            {
                _logger.LogWarning("No ANAF submissions found for invoice {InvoiceId}", invoiceId);
                return null;
            }

            _logger.LogDebug("Latest ANAF submission retrieved for invoice {InvoiceId}: {SubmissionId}, Status: {Status}",
                invoiceId, submission.Id, submission.Status);
            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest ANAF submission for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<InvoiceAnafSubmission?> GetSuccessfulByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving successful ANAF submission for invoice: {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve ANAF submission with empty InvoiceId");
                return null;
            }

            var submission = await _dbContext.InvoiceAnafSubmissions
                .Include(s => s.Invoice)
                .ThenInclude(i => i.Company)
                .Where(s => s.InvoiceId == invoiceId && s.Status == AnafSubmissionStatus.Ok)
                .OrderByDescending(s => s.UploadedAt)
                .FirstOrDefaultAsync(ct);

            if (submission == null)
            {
                _logger.LogWarning("No successful ANAF submission found for invoice {InvoiceId}", invoiceId);
                return null;
            }

            _logger.LogDebug("Successful ANAF submission retrieved for invoice {InvoiceId}: {SubmissionId}",
                invoiceId, submission.Id);
            return submission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving successful ANAF submission for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task AddAsync(InvoiceAnafSubmission submission, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding ANAF submission for invoice {InvoiceId}, uploadIndex: {UploadIndex}",
            submission.InvoiceId, submission.UploadIndex);

        try
        {
            if (submission == null)
            {
                _logger.LogWarning("Attempted to add null ANAF submission");
                throw new ArgumentNullException(nameof(submission), "ANAF submission cannot be null");
            }

            if (submission.InvoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add ANAF submission with empty InvoiceId");
                throw new ArgumentException("InvoiceId cannot be empty", nameof(submission));
            }

            if (string.IsNullOrWhiteSpace(submission.UploadIndex))
            {
                _logger.LogWarning("Attempted to add ANAF submission with empty UploadIndex");
                throw new ArgumentException("UploadIndex cannot be empty", nameof(submission));
            }

            _logger.LogDebug("ANAF submission details - Id: {SubmissionId}, Status: {Status}, RetryCount: {RetryCount}",
                submission.Id, submission.Status, submission.RetryCount);

            await _dbContext.InvoiceAnafSubmissions.AddAsync(submission, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("ANAF submission successfully added: {SubmissionId} for invoice {InvoiceId}",
                submission.Id, submission.InvoiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding ANAF submission for invoice {InvoiceId}",
                submission?.InvoiceId);
            throw;
        }
    }

    public async Task UpdateAsync(InvoiceAnafSubmission submission, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating ANAF submission: {SubmissionId}, Status: {Status}",
            submission.Id, submission.Status);

        try
        {
            if (submission == null)
            {
                _logger.LogWarning("Attempted to update null ANAF submission");
                throw new ArgumentNullException(nameof(submission), "ANAF submission cannot be null");
            }

            if (submission.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update ANAF submission with empty SubmissionId");
                throw new ArgumentException("SubmissionId cannot be empty", nameof(submission));
            }

            _logger.LogDebug("ANAF submission details - Status: {Status}, RetryCount: {RetryCount}, ErrorMessage: {ErrorMessage}",
                submission.Status, submission.RetryCount, submission.ErrorMessage ?? "none");

            _dbContext.InvoiceAnafSubmissions.Update(submission);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("ANAF submission successfully updated: {SubmissionId}", submission.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating ANAF submission {SubmissionId}", submission?.Id);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Saving changes to database context");

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Database changes saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes to database");
            throw;
        }
    }
}
