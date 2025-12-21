using EasyBilling.Application.Interfaces;
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

        public async Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var cleanCui = createClientRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

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

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            var client = new Client
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = anafDetails?.Name ?? createClientRequest.Name,
                CUI = cleanCui,
                Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createClientRequest.Address,
                County = anafDetails?.RegisteredAddress?.County ?? createClientRequest.County,
                RegNumber = anafDetails?.RegistrationNumber ?? createClientRequest.RegNumber,
                IBAN = createClientRequest.IBAN,
                Bank = createClientRequest.Bank
            };
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
