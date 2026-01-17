using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Repositories;

public class ClientRepository(AppDbContext db, ILogger<ClientRepository> logger) : IClientRepository
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<ClientRepository> _logger = logger;

    public async Task<List<Client>> GetAllClientsByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all clients for company {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve clients with empty CompanyId");
                return [];
            }

            var clients = await _db.Clients
                .Where(c => c.CompanyId == companyId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {ClientCount} clients for company {CompanyId}",
                clients.Count, companyId);

            return clients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaginatedResult<Client>> GetClientsByCompanyIdPaginatedAsync(
        Guid companyId,
        ClientPaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated clients for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve paginated clients with empty CompanyId");
                return new PaginatedResult<Client>
                {
                    Items = [],
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                };
            }

            var query = _db.Clients
                .Where(c => c.CompanyId == companyId)
                .AsQueryable();

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, City: {City}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none", filter.City ?? "none", filter.SortBy ?? "none");

            if (filter.HasSearchTerm)
            {
                _logger.LogDebug("Applying search filter: {SearchTerm}", filter.SearchTerm);
                query = query.Where(c =>
                    c.Name.Contains(filter.SearchTerm!) ||
                    c.CUI.Contains(filter.SearchTerm!));
            }

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                _logger.LogDebug("Applying city filter: {City}", filter.City);
                query = query.Where(c => c.City != null && c.City.Contains(filter.City));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            _logger.LogDebug("Total clients matching filters: {TotalCount}", totalCount);

            query = filter.HasSortBy
                ? ApplySorting(query, filter.SortBy!, filter.SortOrder)
                : query.OrderByDescending(c => c.Id);

            var items = await query
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} clients out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                items.Count, totalCount, companyId, filter.PageNumber);

            return new PaginatedResult<Client>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated clients for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Client?> GetByIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving client by ID: {ClientId}", clientId);

        try
        {
            if (clientId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve client with empty ClientId");
                return null;
            }

            var client = await _db.Clients.FindAsync(
                new object?[] { clientId },
                cancellationToken: cancellationToken);

            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found", clientId);
                return null;
            }

            _logger.LogDebug("Client retrieved: {ClientName} (CUI: {CUI})", client.Name, client.CUI);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<Client?> GetByCuiAndCompanyIdAsync(
        string cui,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving client by CUI: {CUI}, Company: {CompanyId}", cui, companyId);

        try
        {
            if (string.IsNullOrWhiteSpace(cui))
            {
                _logger.LogWarning("Attempted to retrieve client with empty CUI");
                return null;
            }

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve client with empty CompanyId");
                return null;
            }

            var client = await _db.Clients
                .FirstOrDefaultAsync(
                    c => c.CUI == cui && c.CompanyId == companyId,
                    cancellationToken);

            if (client == null)
            {
                _logger.LogWarning("Client with CUI {CUI} not found for company {CompanyId}", cui, companyId);
                return null;
            }

            _logger.LogDebug("Client retrieved: {ClientName} (CUI: {CUI}, CompanyId: {CompanyId})",
                client.Name, client.CUI, companyId);

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client by CUI {CUI} for company {CompanyId}", cui, companyId);
            throw;
        }
    }

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding new client: {ClientName} (CUI: {CUI}) to company {CompanyId}",
            client.Name, client.CUI, client.CompanyId);

        try
        {
            if (client == null)
            {
                _logger.LogWarning("Attempted to add null client");
                throw new ArgumentNullException(nameof(client), "Client cannot be null");
            }

            if (string.IsNullOrWhiteSpace(client.Name))
            {
                _logger.LogWarning("Attempted to add client with empty Name");
                throw new ArgumentException("Client name cannot be empty", nameof(client));
            }

            if (string.IsNullOrWhiteSpace(client.CUI))
            {
                _logger.LogWarning("Attempted to add client with empty CUI");
                throw new ArgumentException("Client CUI cannot be empty", nameof(client));
            }

            if (client.CompanyId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add client with empty CompanyId");
                throw new ArgumentException("CompanyId cannot be empty", nameof(client));
            }

            _logger.LogDebug("Client details - Id: {ClientId}, Address: {Address}, County: {County}, City: {City}",
                client.Id, client.Address ?? "none", client.County ?? "none", client.City ?? "none");

            await _db.Clients.AddAsync(client, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Client successfully added: {ClientId} - {ClientName} (CUI: {CUI})",
                client.Id, client.Name, client.CUI);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding client {ClientName} (CUI: {CUI})",
                client?.Name, client?.CUI);
            throw;
        }
    }

    public async Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting client: {ClientId} - {ClientName} (CUI: {CUI}) from company {CompanyId}",
            client.Id, client.Name, client.CUI, client.CompanyId);

        try
        {
            if (client == null)
            {
                _logger.LogWarning("Attempted to delete null client");
                throw new ArgumentNullException(nameof(client), "Client cannot be null");
            }

            if (client.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to delete client with empty Id");
                throw new ArgumentException("Client Id cannot be empty", nameof(client));
            }

            _db.Clients.Remove(client);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Client successfully deleted: {ClientId} - {ClientName} (CUI: {CUI})",
                client.Id, client.Name, client.CUI);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting client {ClientId}", client?.Id);
            throw;
        }
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
