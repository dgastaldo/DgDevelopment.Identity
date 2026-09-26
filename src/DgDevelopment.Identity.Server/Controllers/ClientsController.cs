using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Clients;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Server.Authorization;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/clients")]
public sealed class ClientsController(
    IClientService clientService,
    IAuditService auditService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [RequirePermission("identity-platform.client.read")]
    public async Task<IActionResult> GetClients([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var result = await clientService.GetPagedAsync(search, page, pageSize, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return Ok(new
        {
            items = result.Items.Select(ClientResponse.From),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity-platform.client.read")]
    public async Task<IActionResult> GetClientById(Guid id, [FromQuery] bool allTenants = false, CancellationToken ct = default)
    {
        var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
        var client = await clientService.GetAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
        return client is null ? NotFound() : Ok(ClientResponse.From(client));
    }

    [HttpPost]
    [RequirePermission("identity-platform.client.create")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var (client, clientSecret) = await clientService.CreateAsync(
                request.Name, request.ClientType, request.PlatformId, request.RequireConsent,
                request.GrantTypes ?? [], request.Scopes ?? [], request.RedirectUris ?? [],
                request.PostLogoutRedirectUris ?? [], request.AdminConsentScopes ?? [],
                tenantContext.TenantId, ct).ConfigureAwait(false);
            await auditService.RecordAsync("client.create", AuditOutcome.Success, tenantContext.TenantId, targetId: client.Id.ToString(), targetType: "client", ct: ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetClientById), new { id = client.Id }, new { client = ClientResponse.From(client), clientSecret });
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid client", detail: ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("identity-platform.client.update")]
    public Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientRequest request, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "client.update", allTenants, canSeeAllTenants => clientService.UpdateAsync(
            id, request.Name, request.RequireConsent, request.PlatformId,
            request.GrantTypes ?? [], request.Scopes ?? [], request.RedirectUris ?? [],
            request.PostLogoutRedirectUris ?? [], request.AdminConsentScopes ?? [],
            tenantContext.TenantId, canSeeAllTenants, ct), ct);

    [HttpPost("{id:guid}/regenerate-secret")]
    [RequirePermission("identity-platform.client.update")]
    public async Task<IActionResult> RegenerateSecret(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            var clientSecret = await clientService.RegenerateSecretAsync(id, tenantContext.TenantId, canSeeAllTenants, ct).ConfigureAwait(false);
            await auditService.RecordAsync("client.secret.regenerate", AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "client", ct: ct).ConfigureAwait(false);
            return Ok(new ClientSecretResponse(clientSecret));
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: ex.Message);
        }
    }

    [HttpPut("{id:guid}/active")]
    [RequirePermission("identity-platform.client.update")]
    public Task<IActionResult> SetActive(Guid id, [FromBody] SetClientActiveRequest request, [FromQuery] bool allTenants, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateAsync(id, request.IsActive ? "client.activate" : "client.deactivate", allTenants, canSeeAllTenants => clientService.SetActiveAsync(id, request.IsActive, tenantContext.TenantId, canSeeAllTenants, ct), ct);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity-platform.client.delete")]
    public Task<IActionResult> DeleteClient(Guid id, [FromQuery] bool allTenants, CancellationToken ct)
        => MutateAsync(id, "client.delete", allTenants, canSeeAllTenants => clientService.DeleteAsync(id, tenantContext.TenantId, canSeeAllTenants, ct), ct);

    private async Task<bool> CanSeeAllTenantsAsync(bool requested, CancellationToken ct)
        => requested && await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

    private async Task<IActionResult> MutateAsync(Guid id, string action, bool allTenants, Func<bool, Task> mutation, CancellationToken ct)
    {
        try
        {
            var canSeeAllTenants = await CanSeeAllTenantsAsync(allTenants, ct).ConfigureAwait(false);
            await mutation(canSeeAllTenants).ConfigureAwait(false);
            await auditService.RecordAsync(action, AuditOutcome.Success, tenantContext.TenantId, targetId: id.ToString(), targetType: "client", ct: ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: ex.Message);
        }
    }
}
