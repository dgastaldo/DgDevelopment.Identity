namespace DgDevelopment.Identity.Client.Maui.Pages;

using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.Client.Maui.Services;

public partial class MfaPage : ContentPage
{
    private readonly AuthSession _authSession;
    private readonly IdentityClient _identityClient;

    public MfaPage(AuthSession authSession, IdentityClient identityClient)
    {
        InitializeComponent();
        _authSession = authSession;
        _identityClient = identityClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        Spinner.IsRunning = true;
        ErrorLabel.IsVisible = false;
        try
        {
            await _authSession.EnsureFreshTokenAsync().ConfigureAwait(true);

            var status = await _identityClient.GetTotpStatusAsync().ConfigureAwait(true);
            RenderTotpStatus(status);

            var devices = await _identityClient.GetPushDevicesAsync().ConfigureAwait(true) ?? [];
            RenderDevices(devices);
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "MFA status could not be loaded.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsRunning = false;
        }
    }

    private void RenderTotpStatus(TotpStatusResponse? status)
    {
        QrImage.IsVisible = false;
        SecretLabel.IsVisible = false;
        CodeEntry.IsVisible = false;
        EnableButton.IsVisible = false;

        if (status is { IsEnabled: true })
        {
            TotpStatusLabel.Text = $"Enabled - {status.AvailableBackupCodes} backup codes remaining.";
            EnrollButton.IsVisible = false;
            DisableButton.IsVisible = true;
        }
        else
        {
            TotpStatusLabel.Text = "Not enabled.";
            EnrollButton.IsVisible = true;
            DisableButton.IsVisible = false;
        }
    }

    private async void OnEnrollClicked(object? sender, EventArgs e)
    {
        try
        {
            var enrollment = await _identityClient.EnrollTotpAsync().ConfigureAwait(true);
            if (enrollment is null)
                return;

            SecretLabel.Text = $"Manual entry key: {enrollment.SecretKey}";
            SecretLabel.IsVisible = true;
            QrImage.Source = ImageSource.FromUri(new Uri(enrollment.QrCodeDataUri));
            QrImage.IsVisible = true;
            CodeEntry.IsVisible = true;
            EnableButton.IsVisible = true;
            EnrollButton.IsVisible = false;
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "Enrollment could not be started.";
            ErrorLabel.IsVisible = true;
        }
    }

    private async void OnEnableClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await _identityClient.EnableTotpAsync(CodeEntry.Text ?? string.Empty).ConfigureAwait(true);
            if (result is null)
                return;

            BackupCodesLabel.Text = "Save these backup codes now:\n" + string.Join('\n', result.BackupCodes);
            BackupCodesLabel.IsVisible = true;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "The verification code is invalid.";
            ErrorLabel.IsVisible = true;
        }
    }

    private async void OnDisableClicked(object? sender, EventArgs e)
    {
        await _identityClient.DisableTotpAsync().ConfigureAwait(true);
        BackupCodesLabel.IsVisible = false;
        await LoadAsync().ConfigureAwait(true);
    }

    private void RenderDevices(IReadOnlyCollection<PushDeviceResponse> devices)
    {
        DevicesContainer.Children.Clear();
        if (devices.Count == 0)
        {
            DevicesContainer.Children.Add(new Label { Text = "No push devices registered." });
            return;
        }

        foreach (var device in devices)
        {
            var row = new HorizontalStackLayout { Spacing = 12 };
            row.Children.Add(new Label { Text = $"{device.DeviceName ?? device.Platform} ({device.Platform})", VerticalOptions = LayoutOptions.Center });
            var removeButton = new Button { Text = "Remove" };
            removeButton.Clicked += async (_, _) =>
            {
                await _identityClient.RemovePushDeviceAsync(device.Id).ConfigureAwait(true);
                await LoadAsync().ConfigureAwait(true);
            };
            row.Children.Add(removeButton);
            DevicesContainer.Children.Add(row);
        }
    }

    private async void OnRegisterDeviceClicked(object? sender, EventArgs e)
    {
        // Real push delivery needs a platform push token (Firebase Cloud Messaging on Android,
        // APNs elsewhere) obtained via the platform's push SDK and wired into a real Azure
        // Notification Hub - neither is configured yet. Registering with a locally-generated
        // placeholder token proves the API round-trip works end-to-end today; replace
        // GetPlaceholderPushToken() with the real FCM/APNs token once that's wired up.
        try
        {
            await _identityClient.RegisterPushDeviceAsync(DeviceInfo.Platform.ToString(), GetPlaceholderPushToken(), DeviceInfo.Name).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "The device could not be registered.";
            ErrorLabel.IsVisible = true;
        }
    }

    private static string GetPlaceholderPushToken() => $"placeholder-{Guid.NewGuid():N}";
}
