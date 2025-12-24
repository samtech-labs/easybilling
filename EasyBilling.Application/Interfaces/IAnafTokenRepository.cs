using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IAnafTokenRepository
    {
        Task AddAsync(AnafToken anafToken);
        Task UpdateAsync(AnafToken anafToken);
        Task<AnafToken?> GetByUserIdAsync(Guid userId);
    }
}
