using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class AnafTokenRepoistory(AppDbContext db): IAnafTokenRepository
    {
        private readonly AppDbContext _db = db;

        public async Task AddAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
        {
            await _db.AnafTokens.AddAsync(anafToken, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
        {
            _db.AnafTokens.Update(anafToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<AnafToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.AnafTokens
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        }
    }
}
