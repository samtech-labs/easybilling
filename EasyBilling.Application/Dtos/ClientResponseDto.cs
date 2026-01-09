namespace EasyBilling.Application.Dtos
{
    public class ClientResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string CUI { get; set; } = default!;
        public string? Address { get; set; }
        public string? County { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? RegNumber { get; set; }
        public string? IBAN { get; set; }
        public string? Bank { get; set; }
    }
}
