namespace EasyBilling.Application.Dtos
{
    public class InvoiceLineResponseDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public string Unit { get; set; } = default!;
        public decimal LineTotal => Quantity * UnitPrice;
        public decimal LineTotalWithVat => LineTotal + (LineTotal * VatRate / 100);
    }
}
