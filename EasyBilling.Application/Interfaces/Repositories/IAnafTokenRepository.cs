using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IAnafTokenRepository
    {
        Task AddAsync(AnafToken anafToken, CancellationToken cancellationToken = default);
        Task UpdateAsync(AnafToken anafToken, CancellationToken cancellationToken = default);
        Task<AnafToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
