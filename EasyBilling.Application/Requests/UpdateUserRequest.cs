using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Application.Requests
{
    public class UpdateUserRequest
    {
        [Required]
        public Guid Id { get; set; }

        [MinLength(3)]
        public string? Username { get; set; }

        [MinLength(6)]
        public string? Password { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Role { get; set; }
    }
}
