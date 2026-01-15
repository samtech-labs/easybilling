using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;

namespace EasyBilling.Infrastructure.Repositories;

public class ClientRepository(AppDbContext db) : IClientRepository
{
    private readonly AppDbContext _db = db;

    public async Task<List<Client>> GetAllClientsByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Clients
            .Where(c => c.CompanyId == companyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedResult<Client>> GetClientsByCompanyIdPaginatedAsync(
        Guid companyId,
        ClientPaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Clients
            .Where(c => c.CompanyId == companyId)
            .AsQueryable();

        if (filter.HasSearchTerm)
        {
            query = query.Where(c =>
                c.Name.Contains(filter.SearchTerm!) ||
                c.CUI.Contains(filter.SearchTerm!));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(c => c.City != null && c.City.Contains(filter.City));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = filter.HasSortBy
            ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
            : query.OrderByDescending(c => c.Id);

        var items = await query
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<Client>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Client?> GetByIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Clients.FindAsync(
            new object?[] { clientId },
            cancellationToken: cancellationToken);
    }

    public async Task<Client?> GetByCuiAndCompanyIdAsync(
        string cui,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Clients
            .FirstOrDefaultAsync(
                c => c.CUI == cui && c.CompanyId == companyId,
                cancellationToken);
    }

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await _db.Clients.AddAsync(client, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
    {
        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Client> ApplySorting(
        IQueryable<Client> query,
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
            "city" => isAscending
                ? query.OrderBy(c => c.City)
                : query.OrderByDescending(c => c.City),
            _ => query.OrderByDescending(c => c.Id)
        };
    }
}
