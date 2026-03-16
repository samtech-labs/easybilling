using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Enums;

public class InvoiceResponseDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public DateTime? DueDate { get; set; }
    public string Series { get; set; } = default!;
    public int Number { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalVat { get; set; }
    public decimal GrandTotal { get; set; }
    public InvoiceType Type { get; set; } = InvoiceType.Invoice;
    public Currency Currency { get; set; } = Currency.RON;

    public Guid? OriginalInvoiceId { get; set; }
    public string OriginalInvoiceNumber { get; set; } = default!;


    public CompanyResponseDto Company { get; set; } = default!;
    public ClientResponseDto Client { get; set; } = default!;
    public List<InvoiceLineResponseDto> InvoiceLines { get; set; } = new();
}
