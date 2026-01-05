using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IClientService
    {
        public Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId);
        public Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId);
        public Task DeleteClientAsync(Guid clientId, Guid companyId);
    }
}
