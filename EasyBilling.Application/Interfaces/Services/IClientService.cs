using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services;

public interface IClientService
{
    Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId);

    Task<PaginatedResult<Client>> GetClientsByCompanyIdPaginatedAsync(Guid companyId, ClientPaginationFilter filter);

    Task<Client> CreateClientAsync(CreateClientRequest createClientRequest, Guid companyId);

    Task DeleteClientAsync(Guid clientId, Guid companyId);
}
