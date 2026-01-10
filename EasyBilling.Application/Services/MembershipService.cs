using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services;

public class MembershipService : IMembershipService
{
    private readonly IMembershipRepository _membershipRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMembershipTypeRepository _membershipTypeRepository;

    public MembershipService(
        IMembershipRepository membershipRepository,
        IUserRepository userRepository,
        IMembershipTypeRepository membershipTypeRepository)
    {
        _membershipRepository = membershipRepository;
        _userRepository = userRepository;
        _membershipTypeRepository = membershipTypeRepository;
    }

    public async Task<MembershipResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(id, cancellationToken);

        if (membership == null)
        {
            throw new InvalidOperationException($"Membership with ID '{id}' not found.");
        }

        return MapToDto(membership);
    }

    public async Task<List<MembershipResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var memberships = await _membershipRepository.GetAllAsync(cancellationToken);
        return memberships.Select(MapToDto).ToList();
    }

    public async Task<List<MembershipResponseDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        var memberships = await _membershipRepository.GetByUserIdAsync(userId, cancellationToken);
        return memberships.Select(MapToDto).ToList();
    }

    public async Task<MembershipResponseDto?> GetActiveMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetActiveMembershipByUserIdAsync(userId, cancellationToken);
        return membership != null ? MapToDto(membership) : null;
    }

    public async Task<MembershipResponseDto> AssignMembershipAsync(AssignMembershipRequest request, CancellationToken cancellationToken = default)
    {
        // Validate user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{request.UserId}' not found.");
        }

        // Validate membership type exists
        var membershipType = await _membershipTypeRepository.GetByIdAsync(request.MembershipTypeId, cancellationToken);
        if (membershipType == null)
        {
            throw new InvalidOperationException($"Membership type with ID '{request.MembershipTypeId}' not found.");
        }

        // Check if user already has an active membership and delete it
        var activeMembership = await _membershipRepository.GetActiveMembershipByUserIdAsync(request.UserId, cancellationToken);
        if (activeMembership != null)
        {
            await _membershipRepository.DeleteAsync(activeMembership.Id, cancellationToken);
        }

        // Calculate end date based on membership type duration
        var endDate = request.StartDate.AddDays(membershipType.DurationInDays);

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            MembershipTypeId = request.MembershipTypeId,
            StartDate = request.StartDate,
            EndDate = endDate
        };

        var created = await _membershipRepository.CreateAsync(membership, cancellationToken);
        return MapToDto(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(id, cancellationToken);

        if (membership == null)
        {
            throw new InvalidOperationException($"Membership with ID '{id}' not found.");
        }

        await _membershipRepository.DeleteAsync(id, cancellationToken);
    }

    private static MembershipResponseDto MapToDto(Membership membership)
    {
        var now = DateTime.UtcNow;
        var isActive = membership.StartDate <= now && membership.EndDate >= now;

        return new MembershipResponseDto
        {
            Id = membership.Id,
            UserId = membership.UserId,
            Username = membership.User?.Username ?? string.Empty,
            MembershipTypeId = membership.MembershipTypeId,
            MembershipTypeName = membership.MembershipType?.Name ?? string.Empty,
            Price = membership.MembershipType?.Price ?? 0,
            MaxInvoicesPerMonth = membership.MembershipType?.MaxInvoicesPerMonth ?? 0,
            EFacturaActive = membership.MembershipType?.EFacturaActive ?? false,
            StartDate = membership.StartDate,
            EndDate = membership.EndDate,
            IsActive = isActive
        };
    }
}
