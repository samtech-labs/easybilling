namespace EasyBilling.Application.Dtos
{
    public class InvoiceLineDto
    {
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public string? Unit { get; set; }
    }
}
