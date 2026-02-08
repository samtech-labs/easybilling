using EasyBilling.Domain.Enums;

namespace EasyBilling.Domain.Models;
public class User
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; } = UserRole.USER.ToString();

    public List<Company>? Companies { get; set; } = new List<Company>();
    public AnafToken? AnafToken { get; set; }
    public Membership? Membership { get; set; }

}