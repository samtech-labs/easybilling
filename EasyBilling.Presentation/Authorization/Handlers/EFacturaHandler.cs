using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Presentation.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EasyBilling.Presentation.Authorization.Handlers;

public class EFacturaHandler(IServiceScopeFactory scopeFactory) : AuthorizationHandler<EFacturaRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EFacturaRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Fail();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();

        var canUse = await membershipService.CanUseEFacturaAsync(userId);

        if (canUse)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "E-Factura not available for your membership"));
        }
    }
}
