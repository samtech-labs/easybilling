using Microsoft.EntityFrameworkCore;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Repositories;

public class CompanyRepository(AppDbContext db, ILogger<CompanyRepository> logger) : ICompanyRepository
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<CompanyRepository> _logger = logger;

    public async Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving company by ID: {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve company with empty CompanyId");
                return null;
            }

            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                return null;
            }

            _logger.LogDebug("Company retrieved: {CompanyName} (CUI: {CUI})", company.Name, company.CUI);
            return company;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Company?> GetByCuiAsync(string cui, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving company by CUI: {CUI}, User: {UserId}", cui, userId);

        try
        {
            if (string.IsNullOrWhiteSpace(cui))
            {
                _logger.LogWarning("Attempted to retrieve company with empty CUI");
                return null;
            }

            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve company with empty UserId");
                return null;
            }

            var company = await _db.Companies.FirstOrDefaultAsync(
                c => c.CUI == cui && c.UserId == userId,
                cancellationToken);

            if (company == null)
            {
                _logger.LogWarning("Company with CUI {CUI} not found for user {UserId}", cui, userId);
                return null;
            }

            _logger.LogDebug("Company retrieved: {CompanyName} (CUI: {CUI}, UserId: {UserId})",
                company.Name, company.CUI, userId);

            return company;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company by CUI {CUI} for user {UserId}", cui, userId);
            throw;
        }
    }

    public async Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all companies");

        try
        {
            var companies = await _db.Companies.ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {CompanyCount} companies total", companies.Count);

            return companies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all companies");
            throw;
        }
    }

    public async Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all companies for user {UserId}", userId);

        try
        {
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve companies with empty UserId");
                return [];
            }

            var companies = await _db.Companies
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {CompanyCount} companies for user {UserId}",
                companies.Count, userId);

            return companies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving companies for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PaginatedResult<Company>> GetCompaniesByUserPaginatedAsync(
        Guid userId,
        CompanyPaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated companies for user {UserId} - Page: {PageNumber}, PageSize: {PageSize}",
            userId, filter.PageNumber, filter.PageSize);

        try
        {
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve paginated companies with empty UserId");
                return new PaginatedResult<Company>
                {
                    Items = [],
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                };
            }

            var query = _db.Companies
                .Where(c => c.UserId == userId)
                .AsQueryable();

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, IsVatPayer: {IsVatPayer}, IsEFacturaActive: {IsEFacturaActive}, County: {County}",
                filter.SearchTerm ?? "none",
                filter.IsVatPayer.HasValue ? filter.IsVatPayer.Value.ToString() : "none",
                filter.IsEFacturaActive.HasValue ? filter.IsEFacturaActive.Value.ToString() : "none",
                filter.County ?? "none");

            if (filter.HasSearchTerm)
            {
                _logger.LogDebug("Applying search filter: {SearchTerm}", filter.SearchTerm);
                query = query.Where(c => c.Name.Contains(filter.SearchTerm!));
            }

            if (filter.IsVatPayer.HasValue)
            {
                _logger.LogDebug("Applying VAT payer filter: {IsVatPayer}", filter.IsVatPayer.Value);
                query = query.Where(c => c.IsVatPayer == filter.IsVatPayer.Value);
            }

            if (filter.IsEFacturaActive.HasValue)
            {
                _logger.LogDebug("Applying e-Factura active filter: {IsEFacturaActive}", filter.IsEFacturaActive.Value);
                query = query.Where(c => c.IsEFacturaActive == filter.IsEFacturaActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.County))
            {
                _logger.LogDebug("Applying county filter: {County}", filter.County);
                query = query.Where(c => c.County != null && c.County.Contains(filter.County));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            _logger.LogDebug("Total companies matching filters: {TotalCount}", totalCount);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(c => c.Id);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} companies out of {TotalCount} for user {UserId} (page {PageNumber})",
                items.Count, totalCount, userId, filter.PageNumber);

            return new PaginatedResult<Company>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated companies for user {UserId}", userId);
            throw;
        }
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding new company: {CompanyName} (CUI: {CUI}) for user {UserId}",
            company.Name, company.CUI, company.UserId);

        try
        {
            if (company == null)
            {
                _logger.LogWarning("Attempted to add null company");
                throw new ArgumentNullException(nameof(company), "Company cannot be null");
            }

            if (string.IsNullOrWhiteSpace(company.Name))
            {
                _logger.LogWarning("Attempted to add company with empty Name");
                throw new ArgumentException("Company name cannot be empty", nameof(company));
            }

            if (string.IsNullOrWhiteSpace(company.CUI))
            {
                _logger.LogWarning("Attempted to add company with empty CUI");
                throw new ArgumentException("Company CUI cannot be empty", nameof(company));
            }

            if (company.UserId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add company with empty UserId");
                throw new ArgumentException("UserId cannot be empty", nameof(company));
            }

            _logger.LogDebug("Company details - Id: {CompanyId}, Address: {Address}, County: {County}, IsVatPayer: {IsVatPayer}, IsEFacturaActive: {IsEFacturaActive}",
                company.Id, company.Address ?? "none", company.County ?? "none", company.IsVatPayer, company.IsEFacturaActive);

            await _db.Companies.AddAsync(company, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Company successfully added: {CompanyId} - {CompanyName} (CUI: {CUI})",
                company.Id, company.Name, company.CUI);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding company {CompanyName} (CUI: {CUI})",
                company?.Name, company?.CUI);
            throw;
        }
    }

    public async Task DeleteAsync(Company company, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting company: {CompanyId} - {CompanyName} (CUI: {CUI}) for user {UserId}",
            company.Id, company.Name, company.CUI, company.UserId);

        try
        {
            if (company == null)
            {
                _logger.LogWarning("Attempted to delete null company");
                throw new ArgumentNullException(nameof(company), "Company cannot be null");
            }

            if (company.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to delete company with empty Id");
                throw new ArgumentException("Company Id cannot be empty", nameof(company));
            }

            _db.Companies.Remove(company);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Company successfully deleted: {CompanyId} - {CompanyName} (CUI: {CUI})",
                company.Id, company.Name, company.CUI);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting company {CompanyId}", company?.Id);
            throw;
        }
    }

    private static IQueryable<Company> ApplySorting(
        IQueryable<Company> query,
        string sortBy,
        string sortOrder)
    {
        var isAscending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLower() switch
        {
            "name" => isAscending
                ? query.OrderBy(c => c.Name)
                : query.OrderByDescending(c => c.Name),
            "cui" => isAscending
                ? query.OrderBy(c => c.CUI)
                : query.OrderByDescending(c => c.CUI),
            "county" => isAscending
                ? query.OrderBy(c => c.County)
                : query.OrderByDescending(c => c.County),
            _ => query.OrderByDescending(c => c.Id)
        };
    }
}
