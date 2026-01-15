namespace EasyBilling.Domain.Models;

public class UserContext
{
    public Guid UserId { get; set; }

    public string? Username { get; set; }

    public bool IsAuthenticated { get; set; }
}