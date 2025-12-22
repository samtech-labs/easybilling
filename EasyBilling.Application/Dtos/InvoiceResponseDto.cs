namespace EasyBilling.Application.Dtos
{
    public class InvoiceResponseDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string Series { get; set; } = default!;
        public int Number { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalVat { get; set; }
        public decimal GrandTotal { get; set; }
        public CompanyResponseDto Company { get; set; } = default!;
        public ClientResponseDto Client { get; set; } = default!;
        public List<InvoiceLineResponseDto> InvoiceLines { get; set; } = new();
    }
}
