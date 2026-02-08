using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Application.Requests
{
    public class CreateMembershipTypeRequest
    {
        [Required]
        [MinLength(3)]
        public string Name { get; set; } = default!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
        public decimal Price { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Number of invoices must be at least 1")]
        public int MaxInvoicesPerMonth { get; set; }

        [Required]
        public bool EFacturaActive { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Duration must be at least 1 day")]
        public int DurationInDays { get; set; }
    }
}
