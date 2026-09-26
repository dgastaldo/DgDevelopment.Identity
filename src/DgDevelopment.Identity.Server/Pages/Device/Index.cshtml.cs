namespace DgDevelopment.Identity.Server.Pages.Device;

using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class IndexModel : PageModel
{
    private readonly IDeviceAuthorizationService _deviceAuthorizationService;
    private readonly IUserInteractionService _interaction;
    private readonly ITenantSelectionService _tenantSelectionService;

    public IndexModel(IDeviceAuthorizationService deviceAuthorizationService, IUserInteractionService interaction, ITenantSelectionService tenantSelectionService)
    {
        _deviceAuthorizationService = deviceAuthorizationService;
        _interaction = interaction;
        _tenantSelectionService = tenantSelectionService;
    }

    [BindProperty]
    public string? UserCode { get; set; }

    public DeviceApprovalInfo? Approval { get; private set; }
    public bool Approved { get; private set; }
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? user_code = null)
    {
        if (string.IsNullOrWhiteSpace(user_code))
            return Page();

        return await LookupAsync(user_code).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserCode))
        {
            Message = "Enter the code shown on your device.";
            return Page();
        }

        return await LookupAsync(UserCode).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (string.IsNullOrWhiteSpace(UserCode))
            return Page();

        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = $"{Request.Path}?user_code={Uri.EscapeDataString(UserCode)}";
            return Redirect(_interaction.GetLoginUrl(returnUrl));
        }

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return RedirectToPage("/Error", new { errorCode = "invalid_user" });

        var selection = await _tenantSelectionService.ResolveAsync(userId, null).ConfigureAwait(false);
        if (selection.Denied)
            return RedirectToPage("/Error", new { errorCode = "access_denied", errorDescription = "The user does not belong to any active tenant." });

        // A device belonging to a user with several tenant memberships approves into the first tenant
        // (by name) for now; a full tenant picker for this page is left for a follow-up PR.
        var tenantId = selection.TenantId ?? selection.Choices.OrderBy(t => t.Name, StringComparer.Ordinal).First().Id;

        var approved = await _deviceAuthorizationService.ApproveAsync(UserCode, tenantId, userId).ConfigureAwait(false);
        if (!approved)
        {
            Message = "This code is no longer valid. It may have expired or been used.";
            return Page();
        }

        Approved = true;
        return Page();
    }

    public IActionResult OnPostDenyAsync()
    {
        Message = "Authorization was denied. You can close this page.";
        return Page();
    }

    private async Task<IActionResult> LookupAsync(string userCode)
    {
        UserCode = userCode;
        Approval = await _deviceAuthorizationService.GetApprovalAsync(userCode).ConfigureAwait(false);
        if (Approval == null)
            Message = "This code is invalid or has expired. Check the code and try again.";

        return Page();
    }
}