using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces.Services;

public interface IMembershipTypeService
{
    Task<MembershipTypeResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<MembershipTypeResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MembershipTypeResponseDto> CreateAsync(CreateMembershipTypeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
