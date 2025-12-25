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

        public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            await _db.Invoices.AddAsync(invoice, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
