namespace DgDevelopment.Identity.Client.Maui.Pages;

using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

public partial class MainPage : ContentPage
{
    private readonly AuthSession _authSession;
    private readonly IdentityClient _identityClient;
    private readonly IServiceProvider _services;

    public MainPage(AuthSession authSession, IdentityClient identityClient, IServiceProvider services)
    {
        InitializeComponent();
        _authSession = authSession;
        _identityClient = identityClient;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Spinner.IsRunning = true;
        ErrorLabel.IsVisible = false;
        try
        {
            await _authSession.EnsureFreshTokenAsync().ConfigureAwait(true);
            var me = await _identityClient.GetMeAsync().ConfigureAwait(true);
            WelcomeLabel.Text = me is null ? "Signed in" : $"Signed in as {me.Username}";
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "Could not load your profile.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsRunning = false;
        }
    }

    private async void OnMfaClicked(object? sender, EventArgs e)
        => await Navigation.PushAsync(_services.GetRequiredService<MfaPage>()).ConfigureAwait(true);

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await _authSession.LogoutAsync().ConfigureAwait(true);
        Application.Current!.Windows[0].Page = new NavigationPage(_services.GetRequiredService<LoginPage>());
    }
}
