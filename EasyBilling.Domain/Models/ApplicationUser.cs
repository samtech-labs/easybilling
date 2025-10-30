using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EasyBilling.Domain.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(150)]
        public string CompanyName { get; set; }

        [Required]
        [StringLength(13, MinimumLength = 13)]
        public string TaxId { get; set; }

        [Required]
        [StringLength(30)]
        public string RegistrationNumber { get; set; }

        [EmailAddress]
        public string? BillingEmailAddress { get; set; }

        [Phone]
        public string? BillingPhone { get; set; }

        public CompanyAddress Address { get; set; } = new CompanyAddress();

        public ICollection<Company> ClientCompanies { get; set; } = new List<Company>();
    }
}

