using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IClientRepository
    {
        Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<Client?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default);
        Task<Client?> GetByCuiAndCompanyIdAsync(string cui, Guid companyId, CancellationToken cancellationToken = default);
        Task AddAsync(Client client, CancellationToken cancellationToken = default);
        Task DeleteAsync(Client client, CancellationToken cancellationToken = default);
    }
}
