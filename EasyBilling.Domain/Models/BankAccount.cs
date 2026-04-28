using EasyBilling.Domain.Enums;

namespace EasyBilling.Domain.Models;

public class BankAccount
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public required string BankName { get; set; }
    public required string Iban { get; set; }
    public Currency Currency { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
}
