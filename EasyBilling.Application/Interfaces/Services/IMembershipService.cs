using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces.Services;

public interface IMembershipService
{
    Task<MembershipResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<MembershipResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<MembershipResponseDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MembershipResponseDto?> GetActiveMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MembershipResponseDto> AssignMembershipAsync(AssignMembershipRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
