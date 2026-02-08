using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services
{
    public class ClientService(IClientRepository clientRepository, ICompanyService companyService) : IClientService
    {
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly ICompanyService _companyService = companyService;

        public async Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            return await _clientRepository.GetAllClientsByCompanyIdAsync(companyId);
        }

        public async Task<PaginatedResult<Client>> GetClientsByCompanyIdPaginatedAsync(
            Guid companyId,
            ClientPaginationFilter filter)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            return await _clientRepository.GetClientsByCompanyIdPaginatedAsync(companyId, filter);
        }

        public async Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var cleanCui = createClientRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();
            var isRomanianClient = string.IsNullOrEmpty(createClientRequest.Country) ||
                                   createClientRequest.Country.Equals("RO", StringComparison.OrdinalIgnoreCase) ||
                                   createClientRequest.Country.Equals("Romania", StringComparison.OrdinalIgnoreCase);

            // Check if client CUI matches the company's own CUI
            if (cleanCui == company.CUI.Replace("RO", "").Replace(" ", "").Trim())
            {
                throw new InvalidOperationException("Cannot add yourself as a client.");
            }

            // Check if client with same CUI already exists for this company
            var existingClient = await _clientRepository.GetByCuiAndCompanyIdAsync(cleanCui, companyId);
            if (existingClient != null)
            {
                throw new InvalidOperationException($"A client with CUI '{cleanCui}' already exists.");
            }

            // Validate required fields for international clients
            if (!isRomanianClient)
            {
                if (string.IsNullOrWhiteSpace(createClientRequest.Address))
                {
                    throw new InvalidOperationException("Address is required for international clients.");
                }
                if (string.IsNullOrWhiteSpace(createClientRequest.Country))
                {
                    throw new InvalidOperationException("Country is required for international clients.");
                }
            }

            Client client;

            // Only fetch ANAF details for Romanian clients
            if (isRomanianClient)
            {
                var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

                client = new Client
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Name = anafDetails?.Name ?? createClientRequest.Name,
                    CUI = cleanCui,
                    Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createClientRequest.Address,
                    County = anafDetails?.RegisteredAddress?.County ?? createClientRequest.County,
                    City = anafDetails?.RegisteredAddress?.City ?? createClientRequest.City,
                    Country = anafDetails?.RegisteredAddress?.Country ?? createClientRequest.Country ?? "Romania",
                    RegNumber = anafDetails?.RegistrationNumber ?? createClientRequest.RegNumber,
                    IBAN = createClientRequest.IBAN,
                    Bank = createClientRequest.Bank
                };
            }
            else
            {
                // For international clients, use provided data without ANAF lookup
                client = new Client
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Name = createClientRequest.Name,
                    CUI = cleanCui,
                    Address = createClientRequest.Address,
                    County = createClientRequest.County,
                    City = createClientRequest.City,
                    Country = createClientRequest.Country,
                    RegNumber = createClientRequest.RegNumber,
                    IBAN = createClientRequest.IBAN,
                    Bank = createClientRequest.Bank
                };
            }

            await _clientRepository.AddAsync(client);
            return client;
        }

        public async Task DeleteClientAsync(Guid clientId, Guid companyId)
        {
            var client = await _clientRepository.GetByIdAsync(clientId);

            if (client == null)
            {
                throw new InvalidOperationException($"Client with ID '{clientId}' does not exist.");
            }

            if (client.CompanyId != companyId)
            {
                throw new InvalidOperationException("Client does not belong to the specified company.");
            }

            await _clientRepository.DeleteAsync(client);
        }
    }
}
