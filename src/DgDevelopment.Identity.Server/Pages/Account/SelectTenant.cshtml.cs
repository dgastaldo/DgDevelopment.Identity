using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DgDevelopment.Identity.Server.Pages.Account;

public sealed class SelectTenantModel(ITenantSelectionService tenantSelectionService) : PageModel
{
    [FromQuery]
    public string? ReturnUrl { get; set; }

    public IReadOnlyCollection<TenantOption> Tenants { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync([FromQuery] string? returnUrl)
    {
        ReturnUrl = returnUrl;

        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(returnUrl))
            return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Invalid tenant selection request." });

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        var selection = await tenantSelectionService.ResolveAsync(userId, null).ConfigureAwait(false);
        if (selection.Denied)
            return RedirectToPage("/Error", new { errorCode = "access_denied", errorDescription = "The user does not belong to any active tenant." });

        Tenants = selection.Choices.Select(t => new TenantOption(t.Id, t.Name, BuildRedirectUrl(returnUrl, t.Slug))).ToList();
        return Page();
    }

    private static string BuildRedirectUrl(string returnUrl, string slug)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}tenant={Uri.EscapeDataString(slug)}";
    }
}

public sealed record TenantOption(Guid Id, string Name, string RedirectUrl);
