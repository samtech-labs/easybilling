namespace EasyBilling.Application.Requests
{
    public class CreateInvoiceRequest
    {
        public required Guid CompanyId { get; set; }
        public required string Series { get; set; }
        public int Number { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? ClientId { get; set; }
        public string? ClientCui { get; set; }
        public ClientDetailsRequest? ClientDetails { get; set; }
        public required List<CreateInvoiceLineRequest> InvoiceLines { get; set; }
        public string? Notes { get; set; }
    }

    public class ClientDetailsRequest
    {
        public string? Name { get; set; }
        public string? Cui { get; set; }
        public string? Address { get; set; }
        public string? County { get; set; }
        public string? RegNumber { get; set; }
        public string? Iban { get; set; }
        public string? Bank { get; set; }
    }
}
