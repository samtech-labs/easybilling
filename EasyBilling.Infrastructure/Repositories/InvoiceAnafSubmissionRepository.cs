using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories;

public class InvoiceAnafSubmissionRepository : IInvoiceAnafSubmissionRepository
{
    private readonly AppDbContext _dbContext;

    public InvoiceAnafSubmissionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InvoiceAnafSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.InvoiceAnafSubmissions
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<InvoiceAnafSubmission?> GetByIdWithInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.InvoiceAnafSubmissions
            .Include(s => s.Invoice)
                .ThenInclude(i => i.Company)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<InvoiceAnafSubmission?> GetLatestByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        return await _dbContext.InvoiceAnafSubmissions
            .Where(s => s.InvoiceId == invoiceId)
            .OrderByDescending(s => s.UploadedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<InvoiceAnafSubmission?> GetSuccessfulByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        return await _dbContext.InvoiceAnafSubmissions
            .Where(s => s.InvoiceId == invoiceId && s.Status == AnafSubmissionStatus.Ok)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(InvoiceAnafSubmission submission, CancellationToken ct = default)
    {
        await _dbContext.InvoiceAnafSubmissions.AddAsync(submission, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(InvoiceAnafSubmission submission, CancellationToken ct = default)
    {
        _dbContext.InvoiceAnafSubmissions.Update(submission);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
