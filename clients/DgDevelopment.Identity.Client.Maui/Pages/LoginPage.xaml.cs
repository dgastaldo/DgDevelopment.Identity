namespace DgDevelopment.Identity.Client.Maui.Pages;

using DgDevelopment.Identity.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

public partial class LoginPage : ContentPage
{
    private readonly AuthSession _authSession;
    private readonly IServiceProvider _services;

    public LoginPage(AuthSession authSession, IServiceProvider services)
    {
        InitializeComponent();
        _authSession = authSession;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _authSession.IsAuthenticatedAsync().ConfigureAwait(true))
            await Navigation.PushAsync(_services.GetRequiredService<MainPage>()).ConfigureAwait(true);
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        SignInButton.IsEnabled = false;
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;

        try
        {
            await _authSession.LoginAsync().ConfigureAwait(true);
            await Navigation.PushAsync(_services.GetRequiredService<MainPage>()).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException)
        {
            ErrorLabel.Text = "Sign-in failed. Please try again.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SignInButton.IsEnabled = true;
            Spinner.IsVisible = false;
            Spinner.IsRunning = false;
        }
    }
}
