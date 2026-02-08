using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories;

public class MembershipRepository : IMembershipRepository
{
    private readonly AppDbContext _dbContext;

    public MembershipRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Memberships
            .Include(m => m.User)
            .Include(m => m.MembershipType)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<List<Membership>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Memberships
            .Include(m => m.User)
            .Include(m => m.MembershipType)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Membership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Memberships
            .Include(m => m.MembershipType)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Membership?> GetActiveMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbContext.Memberships
            .Include(m => m.MembershipType)
            .Where(m => m.UserId == userId && m.StartDate <= now && m.EndDate >= now)
            .OrderByDescending(m => m.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Membership> CreateAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        await _dbContext.Memberships.AddAsync(membership, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetByIdAsync(membership.Id, cancellationToken) ?? membership;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membership = await _dbContext.Memberships.FindAsync(new object[] { id }, cancellationToken);
        if (membership != null)
        {
            _dbContext.Memberships.Remove(membership);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
