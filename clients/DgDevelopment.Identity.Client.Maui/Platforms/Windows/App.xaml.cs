namespace DgDevelopment.Identity.Client.Maui.WinUI;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Design", "CA1515", Justification = "Must stay public: XAML source generation (MauiXamlInflator=SourceGen) emits a matching public partial declaration.")]
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
