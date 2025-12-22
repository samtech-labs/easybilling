using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IClientRepository
    {
        Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId);
        Task<Client?> GetByIdAsync(Guid clientId);
        Task<Client?> GetByCuiAndCompanyIdAsync(string cui, Guid companyId);
        Task AddAsync(Client client);
        Task DeleteAsync(Client client);
    }
}
