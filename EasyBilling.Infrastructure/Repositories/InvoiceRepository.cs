using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Repositories;

public class InvoiceRepository(AppDbContext db, ILogger<InvoiceRepository> logger) : IInvoiceRepository
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<InvoiceRepository> _logger = logger;

    public async Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving invoice by ID: {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve invoice with empty InvoiceId");
                return null;
            }

            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found", invoiceId);
                return null;
            }

            _logger.LogDebug("Invoice retrieved: {Series} Nr. {Number}", invoice.Series, invoice.Number);
            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<Invoice?> GetByIdWithDetailsAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving invoice with details by ID: {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve invoice with empty InvoiceId");
                return null;
            }

            var invoice = await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} with details not found", invoiceId);
                return null;
            }

            _logger.LogDebug("Invoice with details retrieved: {Series} Nr. {Number}, Client: {ClientName}, LineCount: {LineCount}",
                invoice.Series, invoice.Number, invoice.Client.Name, invoice.InvoiceLines?.Count ?? 0);
            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice {InvoiceId} with details", invoiceId);
            throw;
        }
    }

    public async Task<List<Invoice>> GetAllByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all invoices for company {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve invoices with empty CompanyId");
                return [];
            }

            var invoices = await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {InvoiceCount} invoices for company {CompanyId}",
                invoices.Count, companyId);

            return invoices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaginatedResult<Invoice>> GetInvoicesByCompanyIdPaginatedAsync(
        Guid companyId,
        InvoicePaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated invoices for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve paginated invoices with empty CompanyId");
                return new PaginatedResult<Invoice>
                {
                    Items = [],
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                };
            }

            var query = _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .AsQueryable();

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, DateFrom: {DateFrom}, DateTo: {DateTo}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none",
                filter.DateFrom?.ToString("yyyy-MM-dd") ?? "none",
                filter.DateTo?.ToString("yyyy-MM-dd") ?? "none",
                filter.SortBy ?? "none");

            if (filter.HasSearchTerm)
            {
                _logger.LogDebug("Applying search filter: {SearchTerm}", filter.SearchTerm);
                query = query.Where(i =>
                    i.Series.Contains(filter.SearchTerm!) ||
                    i.Number.ToString().Contains(filter.SearchTerm!) ||
                    i.Client.Name.Contains(filter.SearchTerm!));
            }

            if (filter.HasDateRange)
            {
                if (filter.DateFrom.HasValue)
                {
                    _logger.LogDebug("Applying DateFrom filter: {DateFrom}", filter.DateFrom.Value.ToString("yyyy-MM-dd"));
                    query = query.Where(i => i.Date >= filter.DateFrom);
                }
                if (filter.DateTo.HasValue)
                {
                    var dateToEndOfDay = filter.DateTo.Value.AddDays(1).AddTicks(-1);
                    _logger.LogDebug("Applying DateTo filter: {DateTo}", filter.DateTo.Value.ToString("yyyy-MM-dd"));
                    query = query.Where(i => i.Date <= dateToEndOfDay);
                }
            }

            var totalCount = await query.CountAsync(cancellationToken);

            _logger.LogDebug("Total invoices matching filters: {TotalCount}", totalCount);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(i => i.Date);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} invoices out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                items.Count, totalCount, companyId, filter.PageNumber);

            return new PaginatedResult<Invoice>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated invoices for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<Invoice>> GetAllCreditNotesByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all credit notes for company {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve credit notes with empty CompanyId");
                return [];
            }

            var creditNotes = await _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.CreditNote)
                .OrderByDescending(i => i.Date)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {CreditNoteCount} credit notes for company {CompanyId}",
                creditNotes.Count, companyId);

            return creditNotes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credit notes for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaginatedResult<Invoice>> GetCreditNotesByCompanyIdPaginatedAsync(
        Guid companyId,
        InvoicePaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated credit notes for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve paginated credit notes with empty CompanyId");
                return new PaginatedResult<Invoice>
                {
                    Items = [],
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                };
            }

            var query = _db.Invoices
                .Include(i => i.Company)
                .Include(i => i.Client)
                .Include(i => i.InvoiceLines)
                .Include(i => i.OriginalInvoice)
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.CreditNote)
                .AsQueryable();

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, DateFrom: {DateFrom}, DateTo: {DateTo}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none",
                filter.DateFrom?.ToString("yyyy-MM-dd") ?? "none",
                filter.DateTo?.ToString("yyyy-MM-dd") ?? "none",
                filter.SortBy ?? "none");

            if (filter.HasSearchTerm)
            {
                _logger.LogDebug("Applying search filter: {SearchTerm}", filter.SearchTerm);
                query = query.Where(i =>
                    i.Series.Contains(filter.SearchTerm!) ||
                    i.Number.ToString().Contains(filter.SearchTerm!) ||
                    i.Client.Name.Contains(filter.SearchTerm!));
            }

            if (filter.HasDateRange)
            {
                if (filter.DateFrom.HasValue)
                {
                    _logger.LogDebug("Applying DateFrom filter: {DateFrom}", filter.DateFrom.Value.ToString("yyyy-MM-dd"));
                    query = query.Where(i => i.Date >= filter.DateFrom);
                }
                if (filter.DateTo.HasValue)
                {
                    var dateToEndOfDay = filter.DateTo.Value.AddDays(1).AddTicks(-1);
                    _logger.LogDebug("Applying DateTo filter: {DateTo}", filter.DateTo.Value.ToString("yyyy-MM-dd"));
                    query = query.Where(i => i.Date <= dateToEndOfDay);
                }
            }

            var totalCount = await query.CountAsync(cancellationToken);

            _logger.LogDebug("Total credit notes matching filters: {TotalCount}", totalCount);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(i => i.Date);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} credit notes out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                items.Count, totalCount, companyId, filter.PageNumber);

            return new PaginatedResult<Invoice>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated credit notes for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Invoice?> GetLastInvoiceByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving last invoice for company {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve last invoice with empty CompanyId");
                return null;
            }

            var invoice = await _db.Invoices
                .Where(i => i.CompanyId == companyId)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Date)
                .ThenByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("No invoices found for company {CompanyId}", companyId);
                return null;
            }

            _logger.LogDebug("Last invoice retrieved: {Series} Nr. {Number}", invoice.Series, invoice.Number);
            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last invoice for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Invoice?> GetLastInvoiceBySeriesAsync(
        Guid companyId,
        string series,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving last invoice by series for company {CompanyId}, series: {Series}", companyId, series);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve last invoice with empty CompanyId");
                return null;
            }

            if (string.IsNullOrWhiteSpace(series))
            {
                _logger.LogWarning("Attempted to retrieve last invoice with empty Series");
                return null;
            }

            var invoice = await _db.Invoices
                .Where(i => i.CompanyId == companyId && i.Series == series)
                .Where(i => i.Type == InvoiceType.Invoice)
                .OrderByDescending(i => i.Number)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("No invoices found for company {CompanyId} with series {Series}", companyId, series);
                return null;
            }

            _logger.LogDebug("Last invoice by series retrieved: {Series} Nr. {Number}", invoice.Series, invoice.Number);
            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last invoice by series for company {CompanyId}, series: {Series}", companyId, series);
            throw;
        }
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding new invoice: {Series} Nr. {Number} for company {CompanyId}",
            invoice.Series, invoice.Number, invoice.CompanyId);

        try
        {
            if (invoice == null)
            {
                _logger.LogWarning("Attempted to add null invoice");
                throw new ArgumentNullException(nameof(invoice), "Invoice cannot be null");
            }

            if (string.IsNullOrWhiteSpace(invoice.Series))
            {
                _logger.LogWarning("Attempted to add invoice with empty Series");
                throw new ArgumentException("Invoice series cannot be empty", nameof(invoice));
            }

            if (invoice.CompanyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add invoice with empty CompanyId");
                throw new ArgumentException("CompanyId cannot be empty", nameof(invoice));
            }

            _logger.LogDebug("Invoice details - Id: {InvoiceId}, Client: {ClientName}, Total: {Total}, LineCount: {LineCount}",
                invoice.Id, invoice.Client?.Name ?? "none", invoice.TotalAmount, invoice.InvoiceLines?.Count ?? 0);

            await _db.Invoices.AddAsync(invoice, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Invoice successfully added: {InvoiceId} - {Series} Nr. {Number}",
                invoice.Id, invoice.Series, invoice.Number);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding invoice {Series} Nr. {Number}",
                invoice?.Series, invoice?.Number);
            throw;
        }
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
