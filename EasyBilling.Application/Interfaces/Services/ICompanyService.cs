using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface ICompanyService
    {
        Task<List<Company>> GetCompaniesByUserAsync(Guid userId);
        Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default);
        Task<Company> CreateCompanyAsync(CreateCompanyRequest createCompanyRequest);
        Task<Company> CreateCompanyForUserAsync(CreateCompanyForUserRequest createCompanyForUserRequest);
        Task<CompanyResponseDto> GetCompanyDetailsFromAnaf(string cui);
        Task<Company?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task DeleteCompanyAsync(Guid companyId);
        Task DeleteCompanyForUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
        Task<Company?> GetCompanyByCifAsync(string cif, CancellationToken cancellationToken = default);
    }
}
