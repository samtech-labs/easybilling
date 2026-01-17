using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EasyBilling.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Middleware;

public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserContextMiddleware> _logger;

    public UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserContext userContext)
    {
        _logger.LogDebug("Processing user context from HTTP context");

        try
        {
            var user = context.User;
            userContext.IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

            _logger.LogDebug("User authentication status: {IsAuthenticated}", userContext.IsAuthenticated);

            var idClaim = user.FindFirst(JwtRegisteredClaimNames.Sub) ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && Guid.TryParse(idClaim.Value, out var userId))
            {
                userContext.UserId = userId;
                _logger.LogDebug("User ID extracted from claims: {UserId}", userId);
            }
            else
            {
                if (userContext.IsAuthenticated)
                {
                    _logger.LogWarning("User is authenticated but UserId could not be extracted from claims");
                }
                else
                {
                    _logger.LogDebug("No user ID found in claims (unauthenticated request)");
                }
            }

            var usernameClaim = user.FindFirst(JwtRegisteredClaimNames.UniqueName) ?? user.FindFirst(ClaimTypes.Name);
            userContext.Username = usernameClaim?.Value;

            if (!string.IsNullOrEmpty(userContext.Username))
            {
                _logger.LogDebug("Username extracted from claims: {Username}", userContext.Username);
            }
            else if (userContext.IsAuthenticated)
            {
                _logger.LogWarning("User is authenticated but username could not be extracted from claims");
            }

            _logger.LogInformation("User context processed - Authenticated: {IsAuthenticated}, UserId: {UserId}, Username: {Username}",
                userContext.IsAuthenticated, userContext.UserId == Guid.Empty ? "none" : userContext.UserId.ToString(), userContext.Username ?? "none");

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user context");
            throw;
        }
    }
}