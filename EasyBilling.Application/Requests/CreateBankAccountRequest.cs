using EasyBilling.Domain.Enums;

namespace EasyBilling.Application.Requests
{
    public class CreateBankAccountRequest
    {
        public required string BankName { get; set; }
        public required string Iban { get; set; }
        public Currency Currency { get; set; } = Currency.RON;
    }
}
