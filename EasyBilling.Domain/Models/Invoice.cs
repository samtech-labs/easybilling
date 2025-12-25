namespace EasyBilling.Domain.Models
{
    public class Invoice
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public required decimal TotalAmount { get; set; }
        public decimal Vat { get; set; } = 0;
        public required string Series { get; set; }
        public required int Number { get; set; }
        public required Guid CompanyId { get; set; }
        public required Guid ClientId { get; set; }

        public Client Client { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<InvoiceLine>? InvoiceLines { get; set; }
        public ICollection<InvoiceAnafSubmission>? AnafSubmissions { get; set; }
    }
}
