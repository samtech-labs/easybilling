namespace EasyBilling.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public Guid Client_Id { get; set; }
    public string Client_Secret { get; set; }
    
}