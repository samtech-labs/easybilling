namespace EasyBilling.Application.Requests
{
    public class CreateCompanyRequest
    {
        public required string Name { get; set; }
        public required string CUI { get; set; }
        public string? Address { get; set; }
        public string? County { get; set; }
        public string? RegNumber { get; set; }
        public string? IBAN { get; set; }
        public string? Bank { get; set; }
    }
}
