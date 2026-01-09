using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces.Services;

public interface IUserService
{
    Task<UserResponseDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserResponseDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserResponseDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserResponseDto> UpdateUserAsync(UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}