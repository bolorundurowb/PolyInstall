using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
            var main = new MainWindow
            {
                Title = m.Metadata.Name + " Setup",
            };
            main.ExtendClientAreaToDecorationsHint = false;
            TrySetInstallerIcon(main);
            desktop.MainWindow = main;
        }

        ApplyTheme(InstallBootstrap.Manifest.Ui.Theme);
        base.OnFrameworkInitializationCompleted();
    }

    private static void TrySetInstallerIcon(Window window)
    {
        try
        {
            const string avares = "avares://PolyInstall.UI/Assets/polyinstall-icon.png";
            using var stream = AssetLoader.Open(new Uri(avares));
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            window.Icon = new WindowIcon(copy);
        }
        catch
        {
            // Optional branding asset; wizard still works without it.
        }
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
