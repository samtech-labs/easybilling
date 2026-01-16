using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EasyBilling.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUserService> _logger;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated
    {
        get
        {
            var isAuth = User?.Identity?.IsAuthenticated ?? false;
            _logger.LogDebug("IsAuthenticated check: {IsAuthenticated}", isAuth);
            return isAuth;
        }
    }

    public Guid UserId
    {
        get
        {
            try
            {
                var claim = User?.FindFirst(JwtRegisteredClaimNames.Sub)
                    ?? User?.FindFirst(ClaimTypes.NameIdentifier);

                if (claim != null && Guid.TryParse(claim.Value, out var userId))
                {
                    _logger.LogDebug("UserId extracted from claims: {UserId}", userId);
                    return userId;
                }

                _logger.LogWarning("Unable to extract valid UserId from claims");
                return Guid.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting UserId from claims");
                return Guid.Empty;
            }
        }
    }

    public string? Username
    {
        get
        {
            try
            {
                var username = User?.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                    ?? User?.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("Unable to extract username from claims");
                    return null;
                }

                _logger.LogDebug("Username extracted from claims: {Username}", username);
                return username;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting username from claims");
                return null;
            }
        }
    }
}
