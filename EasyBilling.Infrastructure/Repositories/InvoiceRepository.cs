using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class InvoiceRepository(AppDbContext db): IInvoiceRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<Invoice?> GetByIdAsync(Guid invoiceId)
        {
            return await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        }

        public async Task<Invoice?> GetByIdWithDetailsAsync(Guid invoiceId)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
        }

        public async Task<List<Invoice>> GetAllByCompanyIdAsync(Guid companyId)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == companyId)
                .OrderByDescending(i => i.Date)
                .ToListAsync();
        }

        public async Task AddAsync(Invoice invoice)
        {
            await _db.Invoices.AddAsync(invoice);
            await _db.SaveChangesAsync();
        }
    }
}
