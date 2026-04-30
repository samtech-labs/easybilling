using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using FluentValidation;

namespace EasyBilling.Application.Services
{
    public class BankAccountService(
        IBankAccountRepository bankAccountRepository,
        ICompanyService companyService,
        UserContext userContext,
        IValidator<CreateBankAccountRequest> createValidator,
        IValidator<UpdateBankAccountRequest> updateValidator) : IBankAccountService
    {
        private readonly IBankAccountRepository _bankAccountRepository = bankAccountRepository;
        private readonly ICompanyService _companyService = companyService;
        private readonly UserContext _userContext = userContext;
        private readonly IValidator<CreateBankAccountRequest> _createValidator = createValidator;
        private readonly IValidator<UpdateBankAccountRequest> _updateValidator = updateValidator;

        public async Task<List<BankAccountResponseDto>> GetByCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            await EnsureCompanyOwnedByCurrentUserAsync(companyId, cancellationToken);

            var bankAccounts = await _bankAccountRepository.GetByCompanyAsync(companyId, cancellationToken);
            return bankAccounts.Select(ToDto).ToList();
        }

        public async Task<BankAccountResponseDto> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var bankAccount = await GetOwnedBankAccountAsync(id, cancellationToken);
            return ToDto(bankAccount);
        }

        public async Task<BankAccountResponseDto> CreateAsync(
            Guid companyId,
            CreateBankAccountRequest request,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
            await EnsureCompanyOwnedByCurrentUserAsync(companyId, cancellationToken);

            var now = DateTime.UtcNow;
            var bankAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                BankName = request.BankName.Trim(),
                Iban = request.Iban.Trim().ToUpperInvariant(),
                Currency = request.Currency,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _bankAccountRepository.AddAsync(bankAccount, cancellationToken);
            return ToDto(bankAccount);
        }

        public async Task<BankAccountResponseDto> UpdateAsync(
            Guid id,
            UpdateBankAccountRequest request,
            CancellationToken cancellationToken = default)
        {
            await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
            var bankAccount = await GetOwnedBankAccountAsync(id, cancellationToken);

            bankAccount.BankName = request.BankName.Trim();
            bankAccount.Iban = request.Iban.Trim().ToUpperInvariant();
            bankAccount.Currency = request.Currency;
            bankAccount.UpdatedAt = DateTime.UtcNow;

            await _bankAccountRepository.UpdateAsync(bankAccount, cancellationToken);
            return ToDto(bankAccount);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var bankAccount = await GetOwnedBankAccountAsync(id, cancellationToken);
            await _bankAccountRepository.DeleteAsync(bankAccount, cancellationToken);
        }

        private async Task<BankAccount> GetOwnedBankAccountAsync(Guid id, CancellationToken cancellationToken)
        {
            var bankAccount = await _bankAccountRepository.GetByIdAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Bank account with ID '{id}' does not exist.");

            await EnsureCompanyOwnedByCurrentUserAsync(bankAccount.CompanyId, cancellationToken);
            return bankAccount;
        }

        private async Task EnsureCompanyOwnedByCurrentUserAsync(Guid companyId, CancellationToken cancellationToken)
        {
            if (_userContext.UserId == Guid.Empty)
            {
                throw new InvalidOperationException("User is not authenticated.");
            }

            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken)
                ?? throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");

            if (company.UserId != _userContext.UserId)
            {
                throw new InvalidOperationException("Company does not belong to the current user.");
            }
        }

        private static BankAccountResponseDto ToDto(BankAccount bankAccount) => new()
        {
            Id = bankAccount.Id,
            CompanyId = bankAccount.CompanyId,
            BankName = bankAccount.BankName,
            Iban = bankAccount.Iban,
            Currency = bankAccount.Currency,
            CreatedAt = bankAccount.CreatedAt,
            UpdatedAt = bankAccount.UpdatedAt
        };
    }
}
