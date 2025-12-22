namespace EasyBilling.Application.Dtos
{
    public class CompanyResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string CUI { get; set; } = default!;
        public string? Address { get; set; }
        public string? County { get; set; }
        public string? RegNumber { get; set; }
        public string? IBAN { get; set; }
        public string? Bank { get; set; }
    }
}
