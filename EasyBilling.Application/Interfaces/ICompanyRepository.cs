using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface ICompanyRepository
    {
        Task<Company?> GetByIdAsync(Guid id);
        Task<List<Company>> GetAllCompaniesAsync();
    }
}
