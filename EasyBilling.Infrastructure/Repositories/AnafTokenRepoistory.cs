using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class AnafTokenRepoistory(AppDbContext db): IAnafTokenRepository
    {
        private readonly AppDbContext _db = db;

        public async Task AddAsync(AnafToken anafToken)
        {
            await _db.AnafTokens.AddAsync(anafToken);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(AnafToken anafToken)
        {
            _db.AnafTokens.Update(anafToken);
            await _db.SaveChangesAsync();
        }

        public async Task<AnafToken?> GetByUserIdAsync(Guid userId)
        {
            return await _db.AnafTokens
                .FirstOrDefaultAsync(t => t.UserId == userId);
        }
    }
}
