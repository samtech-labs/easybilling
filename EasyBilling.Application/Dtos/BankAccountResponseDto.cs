using EasyBilling.Domain.Enums;

namespace EasyBilling.Application.Dtos
{
    public class BankAccountResponseDto
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string BankName { get; set; } = default!;
        public string Iban { get; set; } = default!;
        public Currency Currency { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
