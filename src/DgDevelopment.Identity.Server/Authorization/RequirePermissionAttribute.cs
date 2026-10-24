using DgDevelopment.Identity.Application.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace DgDevelopment.Identity.Server.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission, string? scopeType = null, string? scopeValue = null)
    : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; } = permission;
    public string? ScopeType { get; } = scopeType;
    public string? ScopeValue { get; } = scopeValue;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(subject, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        var evaluator = context.HttpContext.RequestServices.GetRequiredService<IPermissionEvaluator>();
        if (await evaluator.HasPermissionAsync(userId, tenantContext.TenantId, Permission, ScopeType, ScopeValue, context.HttpContext.RequestAborted).ConfigureAwait(false))
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "The authenticated user does not have the required permission."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
