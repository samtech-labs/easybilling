namespace EasyBilling.Domain.Models;

public class InvoiceLine
{
    public Guid Id { get; set; }

    public required string Description { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal VatRate { get; set; }

    public required string Unit { get; set; }

    public required Guid InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
