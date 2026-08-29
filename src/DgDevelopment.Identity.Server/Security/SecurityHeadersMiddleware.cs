namespace DgDevelopment.Identity.Server.Security;

/// <summary>
/// Sets a Content-Security-Policy (nonce-based, so the one inline script in Account/Mfa.cshtml
/// doesn't need 'unsafe-inline') plus a handful of standard hardening headers, on every response.
/// Surveyed the whole Pages tree before writing this policy: no other inline &lt;script&gt; blocks,
/// no inline style="" attributes, no external CDN references - everything is same-origin, so
/// 'self' covers it without further relaxation.
/// </summary>
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var nonce = context.RequestServices.GetRequiredService<CspNonceService>().Nonce;

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["Content-Security-Policy"] = string.Join("; ",
                [
                    "default-src 'self'",
                    $"script-src 'self' 'nonce-{nonce}'",
                    "style-src 'self'",
                    "img-src 'self' data:",
                    "font-src 'self'",
                    "connect-src 'self'",
                    "form-action 'self'",
                    "frame-ancestors 'none'",
                    "base-uri 'self'",
                    "object-src 'none'",
                ]);
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                return Task.CompletedTask;
            });

            await next().ConfigureAwait(false);
        });
    }
}
