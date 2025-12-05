using EasyBilling.Application.Dtos;

namespace EasyBilling.Application.Requests
{
    public class CreateInvoiceRequest
    {
        public ClientDto Client { get; set; } = default!;

        public string Series { get; set; } = default!;
        public int Number { get; set; }

        public DateTime IssueDate { get; set; }
        public DateTime? DueDate { get; set; }

        public string Currency { get; set; } = "RON";
        public List<InvoiceLineDto> Lines { get; set; } = new();
    }
}
