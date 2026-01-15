using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EasyBilling.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace EasyBilling.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var claim = User?.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? User?.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null && Guid.TryParse(claim.Value, out var userId))
                return userId;

            return Guid.Empty;
        }
    }

    public string? Username => User?.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
        ?? User?.FindFirst(ClaimTypes.Name)?.Value;
}
