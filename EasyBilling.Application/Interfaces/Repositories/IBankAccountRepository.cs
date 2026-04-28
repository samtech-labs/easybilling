using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Repositories
{
    public interface IBankAccountRepository
    {
        Task<List<BankAccount>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
        Task UpdateAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
        Task DeleteAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
    }
}
