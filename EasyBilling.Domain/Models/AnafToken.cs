using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyBilling.Domain.Models;

public class AnafToken
{
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public string AccessToken { get; set; } = null!;

    [Required]
    public string RefreshToken { get; set; } = null!;

    [Required]
    public DateTime AccessTokenExpiresAt { get; set; }

    [Required]
    public DateTime RefreshTokenExpiresAt { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
