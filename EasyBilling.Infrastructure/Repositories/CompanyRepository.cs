using Microsoft.EntityFrameworkCore;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Interfaces;
using EasyBilling.Infrastructure.Persistence;

namespace EasyBilling.Infrastructure.Repositories
{
    public class CompanyRepository(AppDbContext db): ICompanyRepository
    {
        public readonly AppDbContext _db = db;

        public async Task<Company?> GetByIdAsync(Guid companyId)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
        }

        public async Task<List<Company>> GetAllCompaniesAsync()
        {
            return await _db.Companies.ToListAsync();
        }
    }
}
