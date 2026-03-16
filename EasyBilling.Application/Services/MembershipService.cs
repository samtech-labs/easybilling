using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services;

public class MembershipService : IMembershipService
{
    private readonly IMembershipRepository _membershipRepository;
    private readonly IUserService _userService;
    private readonly IInvoiceService _invoiceService;
    private readonly IMembershipTypeService _membershipTypeService;

    public MembershipService(
        IMembershipRepository membershipRepository,
        IUserService userService,
        IInvoiceService invoiceService,
        IMembershipTypeService membershipTypeService)
    {
        _membershipRepository = membershipRepository;
        _userService = userService;
        _invoiceService = invoiceService;
        _membershipTypeService = membershipTypeService;
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
        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
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
        var user = await _userService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{request.UserId}' not found.");
        }

        // Validate membership type exists
        var membershipType = await _membershipTypeService.GetByIdAsync(request.MembershipTypeId, cancellationToken);
        if (membershipType == null)
        {
            throw new InvalidOperationException($"Membership type with ID '{request.MembershipTypeId}' not found.");
        }

        // Check if user already has a membership (active or expired) and delete it
        var existingMemberships = await _membershipRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        foreach (var existing in existingMemberships)
        {
            await _membershipRepository.DeleteAsync(existing.Id, cancellationToken);
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

    public async Task<bool> HasMembershipActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetActiveMembershipByUserIdAsync(userId, cancellationToken);
        return membership != null;
    }

    public async Task<bool> CanCreateInvoiceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetActiveMembershipByUserIdAsync(userId, cancellationToken);
        if (membership == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (membership.StartDate > now || membership.EndDate < now)
        {
            return false;
        }

        var membershipType = membership.MembershipType;

        if (membershipType == null)
        {
            return false;
        }

        var invoices = await _invoiceService.GetAllForUserByPeriodAsync(userId, membership.StartDate, membership.EndDate, cancellationToken);

        return invoices.Count < membershipType.MaxInvoicesPerMonth;
    }

    public async Task<bool> CanUseEFacturaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetActiveMembershipByUserIdAsync(userId, cancellationToken);

        if (membership == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        if (membership.StartDate > now || membership.EndDate < now)
        {
            return false;
        }

        var membershipType = membership.MembershipType;

        return membershipType?.EFacturaActive ?? false; 
    }

    public async Task<int> GetInvoicesCreatedThisMembershipAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetActiveMembershipByUserIdAsync(userId, cancellationToken);

        if (membership == null)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        if (membership.StartDate > now || membership.EndDate < now)
        {
            return 0;
        }

        var membershipType = membership.MembershipType;
        if (membershipType == null)
        {
            return 0;
        }

        var invoices = await _invoiceService.GetAllForUserByPeriodAsync(userId, membership.StartDate, membership.EndDate, cancellationToken);

        return invoices.Count;
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
