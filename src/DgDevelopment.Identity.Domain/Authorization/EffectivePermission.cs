namespace DgDevelopment.Identity.Domain.Authorization;

public sealed record EffectivePermission(string Name, string? ScopeType, string? ScopeValue);
