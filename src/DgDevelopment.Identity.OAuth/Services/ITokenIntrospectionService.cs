namespace DgDevelopment.Identity.OAuth.Services;

public interface ITokenIntrospectionService
{
    Task<IntrospectionResponse> IntrospectAsync(string token, string? tokenTypeHint = null, CancellationToken ct = default);
}