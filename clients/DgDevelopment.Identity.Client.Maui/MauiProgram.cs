namespace DgDevelopment.Identity.Client.Maui;

using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.Client.Maui.Services;
using Microsoft.Extensions.Logging;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(AppConfig.CreateOidcOptions());
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<SessionEventClient>();
        builder.Services.AddSingleton<AuthSession>();
        builder.Services.AddTransient<IdentityAuthHandler>();
        builder.Services.AddHttpClient<IdentityClient>().AddHttpMessageHandler<IdentityAuthHandler>();

        builder.Services.AddTransient<Pages.LoginPage>();
        builder.Services.AddTransient<Pages.MainPage>();
        builder.Services.AddTransient<Pages.MfaPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
