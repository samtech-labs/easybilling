namespace EasyBilling.Application.Dtos
{
    public class AnafTokenCreateDto
    {
        public Guid? Id { get; set; }
        public Guid UserId { get; set; }
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAt { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
