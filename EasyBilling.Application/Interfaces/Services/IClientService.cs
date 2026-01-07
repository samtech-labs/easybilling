using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IClientService
    {
        public Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId);
        public Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId);
        public Task DeleteClientAsync(Guid clientId, Guid companyId);
        public Task<PagedResponse<ClientResponseDto>> GetClientsByCompanyIdPagedAsync(Guid companyId, PageRequest page, 
            CancellationToken cancellationToken = default);
    }
}
