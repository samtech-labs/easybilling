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
    }
}
