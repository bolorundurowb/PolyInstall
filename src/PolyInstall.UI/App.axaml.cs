using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using PolyInstall.Core.Hosting;

namespace PolyInstall.UI;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var m = InstallBootstrap.Manifest;
            desktop.MainWindow = new MainWindow
            {
                Title = m.Metadata.Name + " Setup",
            };
            desktop.MainWindow.ExtendClientAreaToDecorationsHint = false;
        }

        ApplyTheme(InstallBootstrap.Manifest.Ui.Theme);
        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyTheme(string theme)
    {
        Current!.RequestedThemeVariant = theme.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
