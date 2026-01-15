using Microsoft.EntityFrameworkCore;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;

namespace EasyBilling.Infrastructure.Repositories;

public class CompanyRepository(AppDbContext db) : ICompanyRepository
{
    private readonly AppDbContext _db = db;

    public async Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetByCuiAsync(string cui, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Companies.FirstOrDefaultAsync(
            c => c.CUI == cui && c.UserId == userId,
            cancellationToken);
    }

    public async Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Companies.ToListAsync(cancellationToken);
    }

    public async Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Companies
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedResult<Company>> GetCompaniesByUserPaginatedAsync(
        Guid userId,
        CompanyPaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Companies
            .Where(c => c.UserId == userId)
            .AsQueryable();

        if (filter.HasSearchTerm)
        {
            query = query.Where(c => c.Name.Contains(filter.SearchTerm!));
        }

        if (filter.IsVatPayer.HasValue)
        {
            query = query.Where(c => c.IsVatPayer == filter.IsVatPayer.Value);
        }

        if (filter.IsEFacturaActive.HasValue)
        {
            query = query.Where(c => c.IsEFacturaActive == filter.IsEFacturaActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.County))
        {
            query = query.Where(c => c.County != null && c.County.Contains(filter.County));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = filter.HasSortBy
            ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
            : query.OrderByDescending(c => c.Id);

        var items = await query
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<Company>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        await _db.Companies.AddAsync(company, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Company company, CancellationToken cancellationToken = default)
    {
        _db.Companies.Remove(company);
        await _db.SaveChangesAsync(cancellationToken);
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
