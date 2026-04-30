using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IBankAccountService
    {
        Task<List<BankAccountResponseDto>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<BankAccountResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<BankAccountResponseDto> CreateAsync(Guid companyId, CreateBankAccountRequest request, CancellationToken cancellationToken = default);
        Task<BankAccountResponseDto> UpdateAsync(Guid id, UpdateBankAccountRequest request, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
