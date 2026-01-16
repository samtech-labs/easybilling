using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Application.Services;

public class ClientService(
    IClientRepository clientRepository,
    ICompanyService companyService,
    ILogger<ClientService> logger) : IClientService
{
    private readonly IClientRepository _clientRepository = clientRepository;
    private readonly ICompanyService _companyService = companyService;
    private readonly ILogger<ClientService> _logger = logger;

    public async Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId)
    {
        _logger.LogInformation("Retrieving all clients for company {CompanyId}", companyId);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID '{CompanyId}' not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Company found: {CompanyName}", company.Name);

            var clients = await _clientRepository.GetAllClientsByCompanyIdAsync(companyId);

            _logger.LogInformation("Retrieved {ClientCount} clients for company {CompanyId} ({CompanyName})",
                clients.Count, companyId, company.Name);

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
        ClientPaginationFilter filter)
    {
        _logger.LogInformation("Retrieving paginated clients for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID '{CompanyId}' not found for pagination query", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none", filter.SortBy ?? "none");

            var result = await _clientRepository.GetClientsByCompanyIdPaginatedAsync(companyId, filter);

            _logger.LogInformation("Retrieved {ItemCount} clients out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                result.Items.Count, result.TotalCount, companyId, result.PageNumber);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated clients for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId)
    {
        _logger.LogInformation("Creating new client for company {CompanyId} with CUI: {CUI}",
            companyId, createClientRequest.CUI);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID '{CompanyId}' not found when creating client", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Company found: {CompanyName} (CUI: {CompanyCUI})", company.Name, company.CUI);

            var cleanCui = createClientRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

            _logger.LogDebug("Cleaned CUI: {CleanCUI} (original: {OriginalCUI})", cleanCui, createClientRequest.CUI);

            var companyCui = company.CUI.Replace("RO", "").Replace(" ", "").Trim();
            if (cleanCui == companyCui)
            {
                _logger.LogWarning("Client CUI {CUI} matches company's own CUI for company {CompanyId}",
                    cleanCui, companyId);
                throw new InvalidOperationException("Cannot add yourself as a client.");
            }

            var existingClient = await _clientRepository.GetByCuiAndCompanyIdAsync(cleanCui, companyId);
            if (existingClient != null)
            {
                _logger.LogWarning("Client with CUI '{CUI}' already exists for company {CompanyId}",
                    cleanCui, companyId);
                throw new InvalidOperationException($"A client with CUI '{cleanCui}' already exists.");
            }

            _logger.LogDebug("Fetching ANAF details for CUI: {CUI}", cleanCui);

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            if (anafDetails != null)
            {
                _logger.LogInformation("ANAF details retrieved for CUI {CUI}: {CompanyName}",
                    cleanCui, anafDetails.Name);
            }
            else
            {
                _logger.LogWarning("No ANAF details found for CUI {CUI}, using provided information",
                    cleanCui);
            }

            var client = new Client
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = anafDetails?.Name ?? createClientRequest.Name,
                CUI = cleanCui,
                Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createClientRequest.Address,
                County = anafDetails?.RegisteredAddress?.County ?? createClientRequest.County,
                City = anafDetails?.RegisteredAddress?.City ?? createClientRequest.City,
                Country = anafDetails?.RegisteredAddress?.Country ?? createClientRequest.Country,
                RegNumber = anafDetails?.RegistrationNumber ?? createClientRequest.RegNumber,
                IBAN = createClientRequest.IBAN,
                Bank = createClientRequest.Bank
            };

            _logger.LogDebug("Creating client entity: Name={ClientName}, CUI={CUI}, City={City}, County={County}",
                client.Name, client.CUI, client.City, client.County);

            await _clientRepository.AddAsync(client);

            _logger.LogInformation("Client successfully created: {ClientId} - {ClientName} (CUI: {CUI}) for company {CompanyId}",
                client.Id, client.Name, client.CUI, companyId);

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client for company {CompanyId} with CUI: {CUI}",
                companyId, createClientRequest.CUI);
            throw;
        }
    }

    public async Task DeleteClientAsync(Guid clientId, Guid companyId)
    {
        _logger.LogInformation("Deleting client {ClientId} from company {CompanyId}", clientId, companyId);

        try
        {
            var client = await _clientRepository.GetByIdAsync(clientId);

            if (client == null)
            {
                _logger.LogWarning("Client with ID '{ClientId}' not found", clientId);
                throw new InvalidOperationException($"Client with ID '{clientId}' does not exist.");
            }

            _logger.LogDebug("Client found: {ClientName} (CUI: {CUI}) belonging to company {BelongsToCompanyId}",
                client.Name, client.CUI, client.CompanyId);

            if (client.CompanyId != companyId)
            {
                _logger.LogWarning("Client {ClientId} ({ClientName}) does not belong to company {CompanyId}, belongs to {ActualCompanyId}",
                    clientId, client.Name, companyId, client.CompanyId);
                throw new InvalidOperationException("Client does not belong to the specified company.");
            }

            await _clientRepository.DeleteAsync(client);

            _logger.LogInformation("Client successfully deleted: {ClientId} - {ClientName} (CUI: {CUI}) from company {CompanyId}",
                clientId, client.Name, client.CUI, companyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId} from company {CompanyId}",
                clientId, companyId);
            throw;
        }
    }
}
