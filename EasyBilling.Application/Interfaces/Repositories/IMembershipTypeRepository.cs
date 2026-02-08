using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IMembershipTypeRepository
    {
        Task<MembershipType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<MembershipType> CreateAsync(MembershipType membershipType, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);
    }
}
