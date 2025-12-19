using Microsoft.EntityFrameworkCore;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Interfaces;
using EasyBilling.Infrastructure.Persistence;

namespace EasyBilling.Infrastructure.Repositories
{
    public class CompanyRepository(AppDbContext db): ICompanyRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<Company?> GetByIdAsync(Guid companyId)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        }

        public async Task<Company?> GetByCuiAsync(string cui, Guid userId)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.CUI == cui && c.UserId == userId);
        }

        public async Task<List<Company>> GetAllCompaniesAsync()
        {
            return await _db.Companies.ToListAsync();
        }

        public async Task<List<Company>> GetAllCompaniesByUserAsync(Guid userId)
        {
            return await _db.Companies
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        public async Task AddAsync(Company company)
        {
            await _db.Companies.AddAsync(company);
            await _db.SaveChangesAsync();
        }
    }
}
