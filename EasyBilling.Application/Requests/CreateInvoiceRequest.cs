namespace EasyBilling.Application.Requests
{
    public class CreateInvoiceRequest
    {
        public string? Series { get; set; }
        public int? Number { get; set; }
        public DateTime? Date { get; set; }
        public Guid? ClientId { get; set; }
        public string? ClientCui { get; set; }
        public required List<CreateInvoiceLineRequest> InvoiceLines { get; set; }
    }
}
