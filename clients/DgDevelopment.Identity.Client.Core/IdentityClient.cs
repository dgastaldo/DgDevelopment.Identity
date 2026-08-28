using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DgDevelopment.Identity.Client.Core;

public sealed class IdentityClient(HttpClient http, OidcOptions options)
{
    public Uri GetAuthorizeUrl(string? state = null, string? codeChallenge = null, string? tenant = null)
    {
        var url = $"{options.Authority}/connect/authorize" +
                  $"?client_id={Uri.EscapeDataString(options.ClientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri!.ToString())}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(string.Join(' ', options.Scopes))}";

        if (state != null)
            url += $"&state={Uri.EscapeDataString(state)}";

        if (codeChallenge != null)
            url += $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";

        if (!string.IsNullOrWhiteSpace(tenant))
            url += $"&tenant={Uri.EscapeDataString(tenant)}";

        return new Uri(url);
    }

    public Uri GetLogoutUrl(string? idTokenHint = null)
    {
        var url = $"{options.Authority}/connect/endsession" +
                  $"?post_logout_redirect_uri={Uri.EscapeDataString(options.PostLogoutRedirectUri!.ToString())}";

        if (idTokenHint != null)
            url += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";

        return new Uri(url);
    }

    public async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, Uri? redirectUri = null, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = (redirectUri ?? options.RedirectUri!).ToString(),
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["code_verifier"] = codeVerifier
        });

        var response = await http.PostAsync(new Uri($"{options.Authority}/connect/token"), content, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        result!.IssuedAt = DateTime.UtcNow;
        return result;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshTokenValue,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret
        });

        var response = await http.PostAsync(new Uri($"{options.Authority}/connect/token"), content, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        result!.IssuedAt = DateTime.UtcNow;
        return result;
    }

    public async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Authority}/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync(UserInfoJsonContext.Default.UserInfo, ct).ConfigureAwait(false);
    }

    public Task<MyTenantsResponse?> GetMyTenantsAsync(CancellationToken ct = default)
        => http.GetFromJsonAsync<MyTenantsResponse>($"{options.Authority}/api/v1/me/tenants", ct);

    public Task<DashboardSummary?> GetDashboardSummaryAsync(bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<DashboardSummary>($"{options.Authority}/api/v1/dashboard/summary?allTenants={allTenants}", ct);

    public Task<PagedUsersResponse?> GetUsersAsync(string? search, int page, int pageSize, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedUsersResponse>($"{options.Authority}/api/v1/users{query}", ct);
    }

    public Task<UserResponse?> GetUserAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<UserResponse>($"{options.Authority}/api/v1/users/{id}?allTenants={allTenants}", ct);

    public async Task<UserResponse?> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/users", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(ct).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> LockUserAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.PostAsync(new Uri($"{options.Authority}/api/v1/users/{id}/lock?allTenants={allTenants}"), null, ct);

    public Task<HttpResponseMessage> UnlockUserAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.PostAsync(new Uri($"{options.Authority}/api/v1/users/{id}/unlock?allTenants={allTenants}"), null, ct);

    public Task<HttpResponseMessage> DeactivateUserAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/users/{id}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> ResetPasswordAsync(Guid id, string password, bool allTenants = false, CancellationToken ct = default)
        => http.PostAsJsonAsync(new Uri($"{options.Authority}/api/v1/users/{id}/reset-password?allTenants={allTenants}"), new { password }, ct);

    public Task<PagedPermissionsResponse?> GetPermissionsAsync(string? search = null, int page = 1, int pageSize = 100, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedPermissionsResponse>($"{options.Authority}/api/v1/permissions{query}", ct);
    }

    public Task<PagedRolesResponse?> GetRolesAsync(string? search, int page, int pageSize, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedRolesResponse>($"{options.Authority}/api/v1/roles{query}", ct);
    }

    public Task<RoleResponse?> GetRoleAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<RoleResponse>($"{options.Authority}/api/v1/roles/{id}?allTenants={allTenants}", ct);

    public async Task<RoleResponse?> CreateRoleAsync(string name, string description, Guid platformId, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/roles", new { name, description, platformId }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RoleResponse>(ct).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> UpdateRoleAsync(Guid id, string name, string description, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/roles/{id}?allTenants={allTenants}"), new { name, description }, ct);

    public Task<HttpResponseMessage> DeleteRoleAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/roles/{id}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> AssignRolePermissionAsync(Guid roleId, Guid permissionId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/roles/{roleId}/permissions/{permissionId}?allTenants={allTenants}"), new { }, ct);

    public Task<HttpResponseMessage> RemoveRolePermissionAsync(Guid roleId, Guid permissionId, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/roles/{roleId}/permissions/{permissionId}?allTenants={allTenants}"), ct);

    public Task<PagedGroupsResponse?> GetGroupsAsync(string? search, int page, int pageSize, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedGroupsResponse>($"{options.Authority}/api/v1/groups{query}", ct);
    }

    public Task<GroupResponse?> GetGroupAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<GroupResponse>($"{options.Authority}/api/v1/groups/{id}?allTenants={allTenants}", ct);

    public async Task<GroupResponse?> CreateGroupAsync(string name, string description, Guid? parentGroupId, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/groups", new { name, description, parentGroupId }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GroupResponse>(ct).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> UpdateGroupAsync(Guid id, string name, string description, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/groups/{id}?allTenants={allTenants}"), new { name, description }, ct);

    public Task<HttpResponseMessage> SetGroupParentAsync(Guid id, Guid? parentGroupId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/groups/{id}/parent?allTenants={allTenants}"), new { parentGroupId }, ct);

    public Task<HttpResponseMessage> DeleteGroupAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/groups/{id}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> AssignGroupRoleAsync(Guid groupId, Guid roleId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/groups/{groupId}/roles/{roleId}?allTenants={allTenants}"), new { }, ct);

    public Task<HttpResponseMessage> RemoveGroupRoleAsync(Guid groupId, Guid roleId, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/groups/{groupId}/roles/{roleId}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> AssignUserRoleAsync(Guid userId, Guid roleId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/roles/{roleId}?allTenants={allTenants}"), new { }, ct);

    public Task<HttpResponseMessage> RemoveUserRoleAsync(Guid userId, Guid roleId, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/roles/{roleId}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> AssignUserPermissionAsync(Guid userId, Guid permissionId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/permissions/{permissionId}?allTenants={allTenants}"), new { }, ct);

    public Task<HttpResponseMessage> RemoveUserPermissionAsync(Guid userId, Guid permissionId, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/permissions/{permissionId}?allTenants={allTenants}"), ct);

    public Task<HttpResponseMessage> AssignUserGroupAsync(Guid userId, Guid groupId, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/groups/{groupId}?allTenants={allTenants}"), null, ct);

    public Task<HttpResponseMessage> RemoveUserGroupAsync(Guid userId, Guid groupId, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/users/{userId}/groups/{groupId}?allTenants={allTenants}"), ct);

    public Task<PagedPlatformsResponse?> GetPlatformsAsync(string? search, int page, int pageSize, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedPlatformsResponse>($"{options.Authority}/api/v1/platforms{query}", ct);
    }

    public Task<PlatformResponse?> GetPlatformAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<PlatformResponse>($"{options.Authority}/api/v1/platforms/{id}?allTenants={allTenants}", ct);

    public async Task<PlatformResponse?> CreatePlatformAsync(string name, string description, PermissionMode permissionMode, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/platforms", new { name, description, permissionMode }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlatformResponse>(ct).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> UpdatePlatformAsync(Guid id, string name, string description, PermissionMode permissionMode, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/platforms/{id}?allTenants={allTenants}"), new { name, description, permissionMode }, ct);

    public Task<HttpResponseMessage> DeletePlatformAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/platforms/{id}?allTenants={allTenants}"), ct);

    public Task<PagedClientsResponse?> GetClientsAsync(string? search, int page, int pageSize, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        return http.GetFromJsonAsync<PagedClientsResponse>($"{options.Authority}/api/v1/clients{query}", ct);
    }

    public Task<ClientResponse?> GetClientAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.GetFromJsonAsync<ClientResponse>($"{options.Authority}/api/v1/clients/{id}?allTenants={allTenants}", ct);

    public async Task<(ClientResponse Client, string ClientSecret)?> CreateClientAsync(
        string name, ClientType clientType, Guid? platformId, bool requireConsent,
        IReadOnlyCollection<string> grantTypes, IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<Uri> redirectUris, IReadOnlyCollection<Uri> postLogoutRedirectUris,
        IReadOnlyCollection<string> adminConsentScopes, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/clients", new
        {
            name,
            clientType,
            platformId,
            requireConsent,
            grantTypes,
            scopes,
            redirectUris,
            postLogoutRedirectUris,
            adminConsentScopes
        }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateClientResponse>(ct).ConfigureAwait(false) is { } result
            ? (result.Client, result.ClientSecret)
            : null;
    }

    public Task<HttpResponseMessage> UpdateClientAsync(
        Guid id, string name, bool requireConsent, Guid? platformId,
        IReadOnlyCollection<string> grantTypes, IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<Uri> redirectUris, IReadOnlyCollection<Uri> postLogoutRedirectUris,
        IReadOnlyCollection<string> adminConsentScopes, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/clients/{id}?allTenants={allTenants}"), new
        {
            name,
            requireConsent,
            platformId,
            grantTypes,
            scopes,
            redirectUris,
            postLogoutRedirectUris,
            adminConsentScopes
        }, ct);

    public async Task<string?> RegenerateClientSecretAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
    {
        using var response = await http.PostAsync(new Uri($"{options.Authority}/api/v1/clients/{id}/regenerate-secret?allTenants={allTenants}"), null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ClientSecretResponse>(ct).ConfigureAwait(false);
        return result?.ClientSecret;
    }

    public Task<HttpResponseMessage> SetClientActiveAsync(Guid id, bool isActive, bool allTenants = false, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/clients/{id}/active?allTenants={allTenants}"), new { isActive }, ct);

    public Task<HttpResponseMessage> DeleteClientAsync(Guid id, bool allTenants = false, CancellationToken ct = default)
        => http.DeleteAsync(new Uri($"{options.Authority}/api/v1/clients/{id}?allTenants={allTenants}"), ct);

    public Task<IReadOnlyCollection<TenantAdminResponse>?> GetTenantsAdminAsync(CancellationToken ct = default)
        => http.GetFromJsonAsync<IReadOnlyCollection<TenantAdminResponse>>($"{options.Authority}/api/v1/tenants", ct);

    public Task<HttpResponseMessage> SetTenantActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
        => http.PutAsJsonAsync(new Uri($"{options.Authority}/api/v1/tenants/{id}/active"), new { isActive }, ct);

    public async Task<TenantAdminResponse?> CreateTenantAsync(string name, string slug, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{options.Authority}/api/v1/tenants", new { name, slug }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantAdminResponse>(ct).ConfigureAwait(false);
    }

    public Task<PagedAuditLogResponse?> GetAuditLogsAsync(
        string? actorType = null, Guid? actorId = null, string? action = null, string? targetId = null,
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 50, bool allTenants = false, CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}&allTenants={allTenants}";
        if (!string.IsNullOrWhiteSpace(actorType))
            query += $"&actorType={Uri.EscapeDataString(actorType)}";
        if (actorId.HasValue)
            query += $"&actorId={actorId}";
        if (!string.IsNullOrWhiteSpace(action))
            query += $"&action={Uri.EscapeDataString(action)}";
        if (!string.IsNullOrWhiteSpace(targetId))
            query += $"&targetId={Uri.EscapeDataString(targetId)}";
        if (from.HasValue)
            query += $"&from={from.Value:O}";
        if (to.HasValue)
            query += $"&to={to.Value:O}";

        return http.GetFromJsonAsync<PagedAuditLogResponse>($"{options.Authority}/api/v1/audit{query}", ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new HttpRequestException($"Request to '{response.RequestMessage?.RequestUri}' failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}

public sealed record DashboardSummary(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("active")] int Active,
    [property: JsonPropertyName("locked")] int Locked,
    [property: JsonPropertyName("createdLast30Days")] int CreatedLast30Days,
    [property: JsonPropertyName("mfaEnabled")] int MfaEnabled);

public sealed record PagedUsersResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<UserResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record UserRoleAssignmentResponse(
    [property: JsonPropertyName("roleId")] Guid RoleId,
    [property: JsonPropertyName("scopeType")] string? ScopeType,
    [property: JsonPropertyName("scopeValue")] string? ScopeValue);

public sealed record UserPermissionAssignmentResponse(
    [property: JsonPropertyName("permissionId")] Guid PermissionId,
    [property: JsonPropertyName("scopeType")] string? ScopeType,
    [property: JsonPropertyName("scopeValue")] string? ScopeValue);

public sealed record UserGroupAssignmentResponse(
    [property: JsonPropertyName("groupId")] Guid GroupId);

public sealed record UserResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("isLocked")] bool IsLocked,
    [property: JsonPropertyName("isSystemAccount")] bool IsSystemAccount,
    [property: JsonPropertyName("requireMfa")] bool RequireMfa,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("roles")] IReadOnlyCollection<UserRoleAssignmentResponse> Roles,
    [property: JsonPropertyName("permissions")] IReadOnlyCollection<UserPermissionAssignmentResponse> Permissions,
    [property: JsonPropertyName("groups")] IReadOnlyCollection<UserGroupAssignmentResponse> Groups);

public sealed record CreateUserRequest(string Username, string Password, string Email, bool IsSystemAccount = false);

public sealed record PagedPermissionsResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<PermissionResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record PermissionResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("platformId")] Guid PlatformId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("isGlobal")] bool IsGlobal);

public sealed record PagedRolesResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<RoleResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record RolePermissionResponse(
    [property: JsonPropertyName("permissionId")] Guid PermissionId,
    [property: JsonPropertyName("scopeType")] string? ScopeType,
    [property: JsonPropertyName("scopeValue")] string? ScopeValue);

public sealed record RoleResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("platformId")] Guid PlatformId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("permissions")] IReadOnlyCollection<RolePermissionResponse> Permissions);

public sealed record PagedGroupsResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<GroupResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record GroupRoleResponse(
    [property: JsonPropertyName("roleId")] Guid RoleId,
    [property: JsonPropertyName("scopeType")] string? ScopeType,
    [property: JsonPropertyName("scopeValue")] string? ScopeValue);

public sealed record GroupResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parentGroupId")] Guid? ParentGroupId,
    [property: JsonPropertyName("roles")] IReadOnlyCollection<GroupRoleResponse> Roles);

public sealed record TenantResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug);

public sealed record TenantAdminResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("isPlatformTenant")] bool IsPlatformTenant,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt);

public sealed record MyTenantsResponse(
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantResponse> Tenants,
    [property: JsonPropertyName("activeTenantId")] Guid ActiveTenantId,
    [property: JsonPropertyName("isGlobalAdministrator")] bool IsGlobalAdministrator);

public enum PermissionMode
{
    AuthOnly,
    IdentityManaged
}

public sealed record PagedPlatformsResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<PlatformResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record PlatformResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("permissionMode")] PermissionMode PermissionMode);

public enum ClientType
{
    Confidential,
    Public
}

public sealed record PagedClientsResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<ClientResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record ClientResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("clientId")] Guid ClientId,
    [property: JsonPropertyName("platformId")] Guid? PlatformId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("clientType")] ClientType ClientType,
    [property: JsonPropertyName("requirePkce")] bool RequirePkce,
    [property: JsonPropertyName("requireConsent")] bool RequireConsent,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("grantTypes")] IReadOnlyCollection<string> GrantTypes,
    [property: JsonPropertyName("scopes")] IReadOnlyCollection<string> Scopes,
    [property: JsonPropertyName("redirectUris")] IReadOnlyCollection<Uri> RedirectUris,
    [property: JsonPropertyName("postLogoutRedirectUris")] IReadOnlyCollection<Uri> PostLogoutRedirectUris,
    [property: JsonPropertyName("adminConsentScopes")] IReadOnlyCollection<string> AdminConsentScopes);

public sealed record CreateClientResponse(
    [property: JsonPropertyName("client")] ClientResponse Client,
    [property: JsonPropertyName("clientSecret")] string ClientSecret);

public sealed record ClientSecretResponse(
    [property: JsonPropertyName("clientSecret")] string ClientSecret);

public sealed record PagedAuditLogResponse(
    [property: JsonPropertyName("items")] IReadOnlyCollection<AuditLogResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public enum AuditOutcome
{
    Success,
    Failure
}

public sealed record AuditLogResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("actorId")] Guid? ActorId,
    [property: JsonPropertyName("actorType")] string? ActorType,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("targetType")] string? TargetType,
    [property: JsonPropertyName("details")] string? Details,
    [property: JsonPropertyName("outcome")] AuditOutcome Outcome,
    [property: JsonPropertyName("ipAddress")] string? IpAddress,
    [property: JsonPropertyName("userAgent")] string? UserAgent,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp);

[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class TokenResponseJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(UserInfo))]
internal sealed partial class UserInfoJsonContext : JsonSerializerContext;
