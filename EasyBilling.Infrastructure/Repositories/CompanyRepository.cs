using Microsoft.EntityFrameworkCore;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Application.Interfaces.Repositories;

namespace EasyBilling.Infrastructure.Repositories
{
    public class CompanyRepository(AppDbContext db): ICompanyRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        }

        public async Task<Company?> GetByCuiAsync(string cui, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.CUI == cui && c.UserId == userId, cancellationToken);
        }

        public async Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
        {
            return await _db.Companies.ToListAsync(cancellationToken);
        }

        public async Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _db.Companies
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
        {
            await _db.Companies.AddAsync(company, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Company company, CancellationToken cancellationToken = default)
        {
            _db.Companies.Remove(company);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<(List<Company> items, long totalCount)> GetCompaniesByUserPagedAsync(Guid userId, int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if(pageSize > 25) pageSize = 25;

            var query = _db.Companies
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Include(c => c.Clients)
                .OrderBy(c => c.Name);
            
            var totalCount = await query.LongCountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            
            return (items, totalCount);
        }
    }
}
