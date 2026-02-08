using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services;

public class UserService(IUserRepository userRepository) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserResponseDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        return MapUserToDto(user);
    }

    public async Task<List<UserResponseDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(MapUserToDto).ToList();
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, out var role))
        {
            throw new InvalidOperationException($"Invalid role: {request.Role}. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}");
        }

        if (await _userRepository.UsernameExistsAsync(request.Username, cancellationToken))
        {
            throw new InvalidOperationException($"Username '{request.Username}' already exists.");
        }

        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{request.Email}' already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Password = request.Password,
            Email = request.Email,
            Role = request.Role
        };

        var createdUser = await _userRepository.CreateAsync(user, cancellationToken);
        return MapUserToDto(createdUser);
    }

    public async Task<UserResponseDto> UpdateUserAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{request.Id}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
        {
            if (await _userRepository.UsernameExistsAsync(request.Username, cancellationToken))
            {
                throw new InvalidOperationException($"Username '{request.Username}' already exists.");
            }
            user.Username = request.Username;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = request.Password; // Note: In production, hash this password!
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
            {
                throw new InvalidOperationException($"Email '{request.Email}' already exists.");
            }
            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                throw new InvalidOperationException($"Invalid role: {request.Role}. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}");
            }
            user.Role = request.Role;
        }

        var updatedUser = await _userRepository.UpdateAsync(user, cancellationToken);
        return MapUserToDto(updatedUser);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        await _userRepository.DeleteAsync(userId, cancellationToken);
    }

    private static UserResponseDto MapUserToDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        };
    }
}