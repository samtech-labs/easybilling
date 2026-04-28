using EasyBilling.Application.Requests;
using FluentValidation;

namespace EasyBilling.Application.Validators
{
    public class CreateBankAccountRequestValidator : AbstractValidator<CreateBankAccountRequest>
    {
        public CreateBankAccountRequestValidator()
        {
            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage("Bank name is required.")
                .MaximumLength(100).WithMessage("Bank name must be at most 100 characters.");

            RuleFor(x => x.Iban)
                .NotEmpty().WithMessage("IBAN is required.")
                .MaximumLength(34).WithMessage("IBAN must be at most 34 characters.")
                .Matches("^RO\\d{2}[A-Z]{4}[A-Za-z0-9]{16}$")
                .WithMessage("IBAN must be a valid Romanian IBAN (24 characters starting with RO).");

            RuleFor(x => x.Currency)
                .IsInEnum().WithMessage("Currency must be a valid value (RON or EUR).");
        }
    }
}
