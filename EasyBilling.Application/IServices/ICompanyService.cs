using EasyBilling.Domain.Models;

namespace EasyBilling.Application.IServices
{
    public interface ICompanyService
    {
        Task<List<Company>> GetCompaniesByUserAsync(Guid userId);
    }
}
