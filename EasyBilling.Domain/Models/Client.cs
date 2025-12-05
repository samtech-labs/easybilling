namespace EasyBilling.Domain.Models;

public class Client
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public required string Name  { get; set; }
    public string? Address  { get; set; }
    public required string CUI  { get; set; }
    public string? RegNumber  { get; set; }
    public string? IBAN {get; set; }
    public string? Bank {get; set; }
    public Company Company { get; set; } = null!;
}