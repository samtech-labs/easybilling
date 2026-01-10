using EasyBilling.Domain.Enums;

namespace EasyBilling.Domain.Models
{
    public class Invoice
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public required decimal TotalAmount { get; set; }
        public decimal Vat { get; set; } = 0;
        public required string Series { get; set; }
        public required int Number { get; set; }
        public InvoiceType Type { get; set; } = InvoiceType.Invoice;

        // For CreditNote, reference the original invoice
        public Guid? OriginalInvoiceId { get; set; }
        public Invoice? OriginalInvoice { get; set; }

        public required Guid CompanyId { get; set; }
        public required Guid ClientId { get; set; }

        public Client Client { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<InvoiceLine>? InvoiceLines { get; set; }
        public ICollection<InvoiceAnafSubmission>? AnafSubmissions { get; set; }
        public ICollection<Invoice> CreditNotes { get; set; } = [];

        public bool IsCreditNote => Type == InvoiceType.CreditNote;
        public bool HasCreditNotes => CreditNotes.Any();
    }
}
