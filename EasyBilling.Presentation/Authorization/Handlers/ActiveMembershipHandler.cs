using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Presentation.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EasyBilling.Presentation.Authorization.Handlers;

public class ActiveMembershipHandler : AuthorizationHandler<ActiveMembershipRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ActiveMembershipHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveMembershipRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Fail();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();

        var hasActive = await membershipService.HasMembershipActiveAsync(userId);

        if (hasActive)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "No active membership"));
        }
    }
}
