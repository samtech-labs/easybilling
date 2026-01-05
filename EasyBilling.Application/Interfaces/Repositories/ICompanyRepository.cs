using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Company?> GetByCuiAsync(string cui, Guid userId, CancellationToken cancellationToken = default);
        Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default);
        Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task AddAsync(Company company, CancellationToken cancellationToken = default);
        Task DeleteAsync(Company company, CancellationToken cancellationToken = default);
    }
}
