using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Application.Requests
{
    public class CreateUserRequest
    {
        [Required]
        [MinLength(3)]
        public string Username { get; set; } = default!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = default!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        public string Role { get; set; } = default!;
    }
}
