namespace EasyBilling.Application.Requests
{
    public class CreateInvoiceLineRequest
    {
        public required string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public required string Unit { get; set; }
    }
}
