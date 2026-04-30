using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories
{
    public class BankAccountRepository(AppDbContext db) : IBankAccountRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<List<BankAccount>> GetByCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            return await _db.BankAccounts
                .Where(b => b.CompanyId == companyId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<BankAccount?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _db.BankAccounts.FindAsync(
                new object?[] { id },
                cancellationToken: cancellationToken);
        }

        public async Task AddAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
        {
            await _db.BankAccounts.AddAsync(bankAccount, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
        {
            _db.BankAccounts.Update(bankAccount);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
        {
            _db.BankAccounts.Remove(bankAccount);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
