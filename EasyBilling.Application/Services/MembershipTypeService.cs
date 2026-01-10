using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services;

public class MembershipTypeService : IMembershipTypeService
{
    private readonly IMembershipTypeRepository _membershipTypeRepository;

    public MembershipTypeService(IMembershipTypeRepository membershipTypeRepository)
    {
        _membershipTypeRepository = membershipTypeRepository;
    }

    public async Task<MembershipTypeResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membershipType = await _membershipTypeRepository.GetByIdAsync(id, cancellationToken);

        if (membershipType == null)
        {
            throw new InvalidOperationException($"Membership type with ID '{id}' not found.");
        }

        return MapToDto(membershipType);
    }

    public async Task<List<MembershipTypeResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var membershipTypes = await _membershipTypeRepository.GetAllAsync(cancellationToken);
        return membershipTypes.Select(MapToDto).ToList();
    }

    public async Task<MembershipTypeResponseDto> CreateAsync(CreateMembershipTypeRequest request, CancellationToken cancellationToken = default)
    {
        if (await _membershipTypeRepository.NameExistsAsync(request.Name, cancellationToken))
        {
            throw new InvalidOperationException($"Membership type '{request.Name}' already exists.");
        }

        var membershipType = new MembershipType
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            MaxInvoicesPerMonth = request.MaxInvoicesPerMonth,
            EFacturaActive = request.EFacturaActive,
            DurationInDays = request.DurationInDays
        };

        var created = await _membershipTypeRepository.CreateAsync(membershipType, cancellationToken);
        return MapToDto(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membershipType = await _membershipTypeRepository.GetByIdAsync(id, cancellationToken);

        if (membershipType == null)
        {
            throw new InvalidOperationException($"Membership type with ID '{id}' not found.");
        }

        await _membershipTypeRepository.DeleteAsync(id, cancellationToken);
    }

    private static MembershipTypeResponseDto MapToDto(MembershipType membershipType)
    {
        return new MembershipTypeResponseDto
        {
            Id = membershipType.Id,
            Name = membershipType.Name,
            Price = membershipType.Price,
            MaxInvoicesPerMonth = membershipType.MaxInvoicesPerMonth,
            EFacturaActive = membershipType.EFacturaActive,
            DurationInDays = membershipType.DurationInDays
        };
    }
}
