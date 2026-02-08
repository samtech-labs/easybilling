using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Application.Requests
{
    public class CreateCompanyForUserRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string CUI { get; set; } = default!;

        public string? Address { get; set; }
        public string? County { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? RegNumber { get; set; }
        public string? IBAN { get; set; }
        public string? Bank { get; set; }
        public bool? IsVatPayer { get; set; }
        public bool? IsEFacturaActive { get; set; }
    }
}
