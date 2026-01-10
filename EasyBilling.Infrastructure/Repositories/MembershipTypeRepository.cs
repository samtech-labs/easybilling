using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories;

public class MembershipTypeRepository : IMembershipTypeRepository
{
    private readonly AppDbContext _dbContext;

    public MembershipTypeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MembershipType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MembershipTypes
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<List<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MembershipTypes
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MembershipType> CreateAsync(MembershipType membershipType, CancellationToken cancellationToken = default)
    {
        await _dbContext.MembershipTypes.AddAsync(membershipType, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return membershipType;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membershipType = await GetByIdAsync(id, cancellationToken);
        if (membershipType != null)
        {
            _dbContext.MembershipTypes.Remove(membershipType);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MembershipTypes.AnyAsync(m => m.Name == name, cancellationToken);
    }
}
