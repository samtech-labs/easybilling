using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Persistence.Repositories
{
    public class CompanyRepository : GenericRepository<Company>, ICompanyRepository
    {
        private readonly AppDbContext _dbContext;

        public CompanyRepository(AppDbContext dbContext)
            : base(dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<IReadOnlyList<Company>> ListByOwnerIdAsync(
            Guid ownerId,
            CancellationToken ct = default)
        {
            return await _dbContext.Companies
                .Where(c => c.ContractorId == ownerId)
                .ToListAsync();
        }

        public async Task<bool> TaxIdExistsForOwnerAsync(Guid ownerId, string taxId, CancellationToken ct = default)
        {
            return await _dbContext.Companies
                .AnyAsync(c =>
                    c.ContractorId == ownerId &&
                    c.TaxId == taxId,
                    ct);
        }
    }
}
