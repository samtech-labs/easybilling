using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid id);
        Task<Company?> GetByCuiAsync(string cui, Guid userId);
        Task<List<Company>> GetAllCompaniesAsync();
        Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId);
        Task AddAsync(Company company);
        Task DeleteAsync(Company company);
    }
}
