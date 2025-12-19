using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<List<Company>> GetCompaniesByUserAsync(Guid userId);
        Task<Company> CreateCompanyAsync(CreateCompanyRequest createCompanyRequest);
        Task<CompanyResponseDto> GetCompanyDetailsFromAnaf(string cui);
    }
}
