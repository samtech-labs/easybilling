using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Interfaces.Repositories;

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
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        public async Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == companyId)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<Invoice?> GetLastInvoiceByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Where(i => i.CompanyId == companyId)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Invoice?> GetLastInvoiceBySeriesAsync(Guid companyId, string series, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Where(i => i.CompanyId == companyId && i.Series == series)
                .OrderByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            await _db.Invoices.AddAsync(invoice, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<(List<Invoice> items, long totalCount)> GetInvoicesPagedAsync(Guid companyId, int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if(pageSize > 25) pageSize = 25;
            var query = _db.Invoices
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId)
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.Number);
            
            var total = await query.LongCountAsync(cancellationToken);
            
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            
            return (items, total);
        }
    }
}
