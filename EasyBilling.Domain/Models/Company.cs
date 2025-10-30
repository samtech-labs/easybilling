using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Domain.Models
{
    public class Company
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid ContractorId { get; set; }
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(13, MinimumLength =13, ErrorMessage ="CUI must be exactly 13 characters.")]
        public string TaxId { get; set; }

        [StringLength(30)]
        public string RegistrationNumber { get; set; } //Registrul comertului
        [EmailAddress]
        public string? EmailAddress { get; set; }
        [Phone]
        public string? Phone { get; set; }
        public CompanyAddress Address { get; set; } = new CompanyAddress();
    }
}
