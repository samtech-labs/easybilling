using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace EasyBilling.Infrastructure.Services;

public class AuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext dbContext, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> AuthenticateAndGenerateTokenAsync(string username, string password)
    {
        _logger.LogInformation("Authentication attempt for user: {Username}", username);

        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("Authentication attempted with empty username");
                return null;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Authentication attempted for user {Username} with empty password", username);
                return null;
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                _logger.LogWarning("Authentication failed: User {Username} not found", username);
                return null;
            }

            if (user.Password != password)
            {
                _logger.LogWarning("Authentication failed: Invalid password for user {Username}", username);
                return null;
            }

            _logger.LogDebug("User {Username} authenticated successfully, generating JWT token", username);

            var jwtSection = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
            };

            var expiresMinutes = double.Parse(jwtSection["ExpiresMinutes"]!);
            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInformation("JWT token successfully generated for user {Username}, expires in {ExpiresMinutes} minutes",
                username, expiresMinutes);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication and token generation for user {Username}", username);
            throw;
        }
    }
}
