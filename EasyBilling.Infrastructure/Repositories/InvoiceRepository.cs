using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class InvoiceRepository(AppDbContext db): IInvoiceRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        public async Task<Invoice?> GetByIdWithDetailsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        public async Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Invoice>> GetAllCreditNotesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.CreditNote)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<Invoice?> GetLastInvoiceByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Invoice?> GetLastInvoiceBySeriesAsync(Guid companyId, string series, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Where(i => i.CompanyId == companyId && i.Series == series)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            await _db.Invoices.AddAsync(invoice, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<Invoice>> GetAllForUserByPeriodAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.Company.UserId == userId && i.Date >= startDate && i.Date <= endDate)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }
    }
}
