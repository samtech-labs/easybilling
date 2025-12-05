namespace EasyBilling.Domain.Models
{
    public class Company
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? Address { get; set; }
        public string? RegNumber { get; set; }
        public required string CUI { get; set; }
        public string? IBAN { get; set; }
        public string? Bank { get; set; }
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;
        public ICollection<Client>? Clients { get; set; }
    }
}
