using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using EasyBilling.Domain.Models;
using Microsoft.AspNetCore.Http;

namespace EasyBilling.Infrastructure.Middleware
{
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, UserContext userContext)
        {
            var user = context.User;
            userContext.IsAuthenticated = user.Identity?.IsAuthenticated ?? false;

            var idClaim = user.FindFirst(JwtRegisteredClaimNames.Sub) ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim != null && Guid.TryParse(idClaim.Value, out var userId))
                userContext.UserId = userId;

            userContext.Username = user.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? user.FindFirst(ClaimTypes.Name)?.Value;

            await _next(context);
        }
    }
}