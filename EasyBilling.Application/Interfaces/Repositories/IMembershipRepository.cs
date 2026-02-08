using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IMembershipRepository
    {
        Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<Membership>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<Membership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Membership?> GetActiveMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Membership> CreateAsync(Membership membership, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
