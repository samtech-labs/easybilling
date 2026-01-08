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
    }
}
