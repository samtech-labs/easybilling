using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class InvoiceRepository(AppDbContext db) : IInvoiceRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        public async Task<Invoice?> GetByIdWithDetailsAsync(
            Guid invoiceId,
            CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Include(i => i.BankAccount)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        }

        public async Task<List<Invoice>> GetAllByCompanyIdAsync(
            Guid companyId, 
            CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.BankAccount)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<PaginatedResult<Invoice>> GetInvoicesByCompanyIdPaginatedAsync(
            Guid companyId,
            InvoicePaginationFilter filter,
            CancellationToken cancellationToken = default)
        {
            var query = _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.BankAccount)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .AsQueryable();

            if (filter.HasSearchTerm)
            {
                query = query.Where(i =>
                    i.Series.Contains(filter.SearchTerm!) ||
                    i.Number.ToString().Contains(filter.SearchTerm!) ||
                    i.Client.Name.Contains(filter.SearchTerm!));
            }

            if (filter.HasDateRange)
            {
                if (filter.DateFrom.HasValue)
                {
                    query = query.Where(i => i.Date >= filter.DateFrom);
                }
                if (filter.DateTo.HasValue)
                {
                    var dateToEndOfDay = filter.DateTo.Value.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.Date <= dateToEndOfDay);
                }
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(i => i.Date);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedResult<Invoice>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<Invoice>> GetAllCreditNotesByCompanyIdAsync(
            Guid companyId, 
            CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Include(i => i.BankAccount)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.CreditNote)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<PaginatedResult<Invoice>> GetCreditNotesByCompanyIdPaginatedAsync(
            Guid companyId,
            InvoicePaginationFilter filter,
            CancellationToken cancellationToken = default)
        {
            var query = _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Include(i => i.BankAccount)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.CreditNote)
                .AsQueryable();

            if (filter.HasSearchTerm)
            {
                query = query.Where(i =>
                    i.Series.Contains(filter.SearchTerm!) ||
                    i.Number.ToString().Contains(filter.SearchTerm!) ||
                    i.Client.Name.Contains(filter.SearchTerm!));
            }

            if (filter.HasDateRange)
            {
                if (filter.DateFrom.HasValue)
                {
                    query = query.Where(i => i.Date >= filter.DateFrom);
                }
                if (filter.DateTo.HasValue)
                {
                    var dateToEndOfDay = filter.DateTo.Value.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.Date <= dateToEndOfDay);
                }
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(i => i.Date);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedResult<Invoice>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<Invoice?> GetLastInvoiceByCompanyIdAsync(
            Guid companyId, 
            CancellationToken cancellationToken = default)
        {
            return await _db.Invoices
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Invoice?> GetLastInvoiceBySeriesAsync(
            Guid companyId, 
            string series, 
            CancellationToken cancellationToken = default)
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

        private static IQueryable<Invoice> ApplySorting(
            IQueryable<Invoice> query, 
            string sortBy, 
            string sortOrder)
        {
            var isAscending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "date" => isAscending 
                    ? query.OrderBy(i => i.Date) 
                    : query.OrderByDescending(i => i.Date),
                "number" => isAscending 
                    ? query.OrderBy(i => i.Number) 
                    : query.OrderByDescending(i => i.Number),
                "total" => isAscending 
                    ? query.OrderBy(i => i.TotalAmount) 
                    : query.OrderByDescending(i => i.TotalAmount),
                "client" => isAscending 
                    ? query.OrderBy(i => i.Client.Name) 
                    : query.OrderByDescending(i => i.Client.Name),
                _ => query.OrderByDescending(i => i.Date)
            };
        }
    }
}
