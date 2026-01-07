using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IClientRepository
    {
        Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<Client?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default);
        Task<Client?> GetByCuiAndCompanyIdAsync(string cui, Guid companyId, CancellationToken cancellationToken = default);
        Task AddAsync(Client client, CancellationToken cancellationToken = default);
        Task DeleteAsync(Client client, CancellationToken cancellationToken = default);
        Task<(List<Client> items, long totalCount)> GetAllClientsByCompanyIdPagedAsync(Guid companyId, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
