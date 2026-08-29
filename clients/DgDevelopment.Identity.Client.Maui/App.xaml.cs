namespace DgDevelopment.Identity.Client.Maui;

using DgDevelopment.Identity.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services, AuthSession authSession)
    {
        ArgumentNullException.ThrowIfNull(authSession);
        InitializeComponent();
        _services = services;
        authSession.ForceLoggedOut += OnForceLoggedOutAsync;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(new NavigationPage(_services.GetRequiredService<Pages.LoginPage>()));

    // The server pushed a force-logout event (password changed elsewhere) - AuthSession has
    // already cleared the stored tokens by the time this fires; this just gets the UI back to the
    // login page. Runs on whatever thread SignalR delivered the message on, so navigation has to
    // be marshaled onto the main thread.
    private Task OnForceLoggedOutAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Windows.Count > 0)
                Windows[0].Page = new NavigationPage(_services.GetRequiredService<Pages.LoginPage>());
        });

        return Task.CompletedTask;
    }
}
