namespace DgDevelopment.Identity.IdentityPlatform.Security;

/// <summary>
/// Sets a Content-Security-Policy plus a handful of standard hardening headers, on every
/// response. Not shared with DgDevelopment.Identity.Server's own SecurityHeadersMiddleware - the
/// two apps' CSP needs differ enough (this one is a Blazor Server + WebAssembly hybrid, Server is
/// plain server-rendered Razor Pages/MVC) that forcing a common implementation would mean either
/// relaxing Server's stricter policy or adding WASM/SignalR allowances it doesn't need.
///
/// No nonce mechanism here (unlike Server's CspNonceService) - App.razor has no inline
/// &lt;script&gt;/&lt;style&gt; of its own to allow. The one open question is Blazor's own
/// &lt;ImportMap /&gt; component, which can render an inline &lt;script type="importmap"&gt; - per
/// the CSP3 spec an import map isn't a "classic/module script" so script-src shouldn't apply to
/// it, but browser behavior here hasn't been verified against a real deployment (no dotnet/browser
/// tooling available in the environment this was written in). If it does get blocked, add a nonce
/// scoped to just that tag rather than relaxing script-src to 'unsafe-inline'.
/// </summary>
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = string.Join("; ",
                    "default-src 'self'",
                    // 'wasm-unsafe-eval': required for the Blazor WebAssembly runtime (Mono/.NET
                    // interpreter) to instantiate its WASM module - Server never needs this, it's
                    // entirely server-rendered.
                    "script-src 'self' 'wasm-unsafe-eval'",
                    "style-src 'self'",
                    "img-src 'self' data:",
                    "font-src 'self'",
                    // Same-origin WebSocket (the Blazor Server SignalR circuit) is covered by
                    // 'self' without needing an explicit wss: scheme entry.
                    "connect-src 'self'",
                    "form-action 'self'",
                    "frame-ancestors 'none'",
                    "base-uri 'self'",
                    "object-src 'none'");
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                return Task.CompletedTask;
            });

            await next().ConfigureAwait(false);
        });
    }
}
