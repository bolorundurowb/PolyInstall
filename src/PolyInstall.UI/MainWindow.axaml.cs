using System.Security.Principal;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;
using SkiaSharp;
using Svg.Skia;

namespace PolyInstall.UI;

public partial class MainWindow : Window
{
    private const string DefaultBrandLogoSvgAvares = "avares://PolyInstall.UI/Assets/polyinstall-logo.svg";
    private const string DefaultBrandLogoPngAvares = "avares://PolyInstall.UI/Assets/polyinstall-icon.png";
    private int _stepIndex;
    private TextBox? _destinationBox;
    private TextBlock? _progressText;
    private ProgressBar? _progressBar;
    private readonly List<WizardStep> _steps;
    private CancellationTokenSource? _installCts;
    private bool _installInProgress;
    private bool _installTouchedDisk;
    private bool _installCreatedInstallDirectory;
    private string? _activeInstallDirectory;

    public MainWindow()
    {
        InitializeComponent();
        TrySetBrandingImage();
        _steps = InstallBootstrap.Manifest.Ui.WizardSteps.Count > 0
            ? InstallBootstrap.Manifest.Ui.WizardSteps
            : DefaultSteps();
        RenderStep(0);
    }

    private void TrySetBrandingImage()
    {
        var manifestPath = InstallBootstrap.Manifest.Ui.LogoPath?.Trim();
        var logo = TryLoadBitmapFromManifestPath(manifestPath)
                   ?? TryLoadBitmapFromAvares(DefaultBrandLogoSvgAvares)
                   ?? TryLoadBitmapFromAvares(DefaultBrandLogoPngAvares);
        if (logo is null)
        {
            BrandLogoImage.IsVisible = false;
            return;
        }

        BrandLogoImage.Source = logo;
        BrandLogoImage.IsVisible = true;
    }

    private static Bitmap? TryLoadBitmapFromManifestPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        try
        {
            var full = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(InstallBootstrap.ExtractRoot, configuredPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                return null;
            if (Path.GetExtension(full).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                return TryLoadSvgBitmapFromFile(full);
            return TryLoadBitmapFromFile(full);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryLoadBitmapFromAvares(string avaresPath)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(avaresPath));
            if (avaresPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return TryLoadSvgBitmapFromStream(stream);
            return CreateBitmapCopy(stream);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryLoadBitmapFromFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return CreateBitmapCopy(stream);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryLoadSvgBitmapFromFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return TryLoadSvgBitmapFromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryLoadSvgBitmapFromStream(Stream stream)
    {
        try
        {
            var svg = new SKSvg();
            svg.Load(stream);
            var picture = svg.Picture;
            if (picture is null)
                return null;

            var bounds = picture.CullRect;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
                return null;
            using var output = new MemoryStream(data.ToArray());
            return new Bitmap(output);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap CreateBitmapCopy(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return new Bitmap(copy);
    }

    private static List<WizardStep> DefaultSteps() =>
    [
        new WizardStep { Type = "welcome", Title = "Welcome" },
        new WizardStep { Type = "finish", Title = "Done" },
    ];

    private void RenderStep(int index)
    {
        _stepIndex = index;
        BackButton.IsEnabled = index > 0;
        var step = _steps[index];
        StepTitle.Text = step.Title ?? step.Type;
        StepContent.Content = BuildStepUi(step);
        NextButton.Content = index == _steps.Count - 1 ? "Close" : "Next";
    }

    private Control BuildStepUi(WizardStep step)
    {
        var pal = InstallBootstrap.Pal;
        return step.Type.Trim().ToLowerInvariant() switch
        {
            "welcome" => new TextBlock
            {
                Text = $"Welcome to {InstallBootstrap.Manifest.Metadata.Name} {InstallBootstrap.Manifest.Metadata.Version}.",
                TextWrapping = TextWrapping.Wrap,
            },
            "eula" => BuildEula(step),
            "destination" => BuildDestination(step, pal),
            "progress" => BuildProgress(),
            "finish" => new TextBlock
            {
                Text = InstallBootstrap.InstallDirectory is null
                    ? "Installation was not run."
                    : $"Installed to:\n{InstallBootstrap.InstallDirectory}",
                TextWrapping = TextWrapping.Wrap,
            },
            _ => new TextBlock { Text = $"Unknown step type: {step.Type}" },
        };
    }

    private Control BuildEula(WizardStep step)
    {
        var path = step.Source;
        if (string.IsNullOrWhiteSpace(path))
            return new TextBlock { Text = "No EULA source configured." };
        var full = Path.IsPathRooted(path)
            ? path
            : Path.Combine(InstallBootstrap.ExtractRoot, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
            return new TextBlock { Text = $"EULA file not found: {full}" };
        return new ScrollViewer
        {
            MaxHeight = 280,
            Content = new TextBlock
            {
                Text = File.ReadAllText(full),
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private Control BuildDestination(WizardStep step, IPolyInstallPal pal)
    {
        var def = step.DefaultPath ?? Path.Combine(pal.ProgramFiles, InstallBootstrap.Manifest.Metadata.Name);
        def = InstallPathResolver.Expand(def, pal);
        _destinationBox = new TextBox { Text = def, Watermark = "Install folder" };
        return new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "Choose installation directory:" }, _destinationBox } };
    }

    private Control BuildProgress()
    {
        _progressText = new TextBlock { Text = "Preparing…" };
        _progressBar = new ProgressBar { IsIndeterminate = true, Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                _progressText,
                _progressBar,
            },
        };
    }

    private async void OnNext(object? sender, RoutedEventArgs e)
    {
        var step = _steps[_stepIndex];
        if (step.Type.Equals("destination", StringComparison.OrdinalIgnoreCase) && _destinationBox is { } destBox)
            InstallBootstrap.InstallDirectory = (destBox.Text ?? string.Empty).Trim();

        if (_stepIndex >= _steps.Count - 1)
        {
            Close();
            return;
        }

        var nextIndex = _stepIndex + 1;
        RenderStep(nextIndex);
        if (_steps[nextIndex].Type.Equals("progress", StringComparison.OrdinalIgnoreCase))
            await RunInstallAsync();
    }

    private async Task RunInstallAsync()
    {
        _installCts = new CancellationTokenSource();
        _installInProgress = true;
        _installTouchedDisk = false;
        _installCreatedInstallDirectory = false;
        _activeInstallDirectory = null;
        BackButton.IsEnabled = false;
        NextButton.IsEnabled = false;

        try
        {
            var manifest = InstallBootstrap.Manifest;
            var pal = InstallBootstrap.Pal;
            var dest = InstallBootstrap.InstallDirectory;
            if (string.IsNullOrWhiteSpace(dest))
                throw new InvalidOperationException("No install directory.");
            dest = InstallPathResolver.Expand(dest.Trim(), pal);
            _activeInstallDirectory = dest;
            var existedBefore = Directory.Exists(dest);
            Directory.CreateDirectory(dest);
            _installTouchedDisk = true;
            _installCreatedInstallDirectory = !existedBefore;

            _installCts.Token.ThrowIfCancellationRequested();
            TaskEngine.RunPhase(manifest.Tasks?.PreInstall, pal);
            _installCts.Token.ThrowIfCancellationRequested();

            await Task.Run(() => DirectoryCopy.CopyRecursive(InstallBootstrap.ExtractRoot, dest, _installCts.Token), _installCts.Token);
            _installCts.Token.ThrowIfCancellationRequested();
            TaskEngine.RunPhase(manifest.Tasks?.PostInstall, pal);
            _installCts.Token.ThrowIfCancellationRequested();
            if (OperatingSystem.IsWindows())
            {
                var win = manifest.Build.Windows ?? new WindowsBuildOptions();
                if (win.RegisterArp)
                {
                    _installCts.Token.ThrowIfCancellationRequested();
                    FinalizeWindowsInstall(manifest, dest);
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_progressText is not null)
                    _progressText.Text = "Installation complete.";
                if (_progressBar is not null)
                {
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = 100;
                }
                NextButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            });
        }
        catch (OperationCanceledException)
        {
            await HandleInstallCancellationAsync();
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_progressText is not null)
                    _progressText.Text = "Error: " + ex.Message;
                if (_progressBar is not null)
                    _progressBar.IsIndeterminate = false;
                CancelButton.IsEnabled = false;
            });
        }
        finally
        {
            _installInProgress = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        if (_stepIndex > 0)
            RenderStep(_stepIndex - 1);
    }

    private async void OnCancel(object? sender, RoutedEventArgs e)
    {
        var confirm = await ShowChoiceDialogAsync(
            "Cancel setup",
            "Are you sure you want to cancel setup?",
            defaultChoice: "No",
            "Yes",
            "No");
        if (!string.Equals(confirm, "Yes", StringComparison.Ordinal))
            return;

        if (!_installInProgress)
        {
            Close();
            return;
        }

        if (_progressText is not null)
            _progressText.Text = "Cancelling…";
        if (_progressBar is not null)
            _progressBar.IsIndeterminate = true;
        CancelButton.IsEnabled = false;
        _installCts?.Cancel();
    }

    private async Task HandleInstallCancellationAsync()
    {
        if (_progressText is not null)
            _progressText.Text = "Setup was cancelled.";
        if (_progressBar is not null)
            _progressBar.IsIndeterminate = false;

        if (!_installTouchedDisk || string.IsNullOrWhiteSpace(_activeInstallDirectory))
        {
            Close();
            return;
        }

        var choice = await ShowChoiceDialogAsync(
            "Setup cancelled",
            "Setup was cancelled. Some files may remain.",
            defaultChoice: "Close",
            "Run cleanup",
            "Close");

        if (!string.Equals(choice, "Run cleanup", StringComparison.Ordinal))
        {
            Close();
            return;
        }

        if (_progressText is not null)
            _progressText.Text = "Running cleanup…";
        await Task.Run(() => CleanupPartialInstall(_activeInstallDirectory!));

        await ShowChoiceDialogAsync(
            "Cleanup complete",
            "Cleanup finished. Setup will now close.",
            defaultChoice: "Close",
            "Close");
        Close();
    }

    private void CleanupPartialInstall(string installDirectory)
    {
        if (_installCreatedInstallDirectory)
        {
            TryDeleteDirectoryRecursive(installDirectory);
            return;
        }

        TryCleanupArpRegistration(installDirectory);
        RemoveCopiedPayloadFiles(InstallBootstrap.ExtractRoot, installDirectory);
        TryDeleteFile(InstallStatePaths.UninstallExePath(installDirectory));
        TryDeleteDirectoryRecursive(InstallStatePaths.PolyDir(installDirectory));
        TryDeleteEmptyDirectoriesForPayload(InstallBootstrap.ExtractRoot, installDirectory);
    }

    private static void RemoveCopiedPayloadFiles(string sourceRoot, string destRoot)
    {
        if (!Directory.Exists(sourceRoot) || !Directory.Exists(destRoot))
            return;

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile);
            var destFile = Path.Combine(destRoot, relative);
            TryDeleteFile(destFile);
        }
    }

    private static void TryDeleteEmptyDirectoriesForPayload(string sourceRoot, string destRoot)
    {
        if (!Directory.Exists(sourceRoot) || !Directory.Exists(destRoot))
            return;

        var sourceDirs = Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderByDescending(static d => d.Length)
            .Select(sourceDir => Path.Combine(destRoot, Path.GetRelativePath(sourceRoot, sourceDir)));
        foreach (var dir in sourceDirs)
            TryDeleteEmptyDirectory(dir);
    }

    private static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).FirstOrDefault() is null)
                Directory.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryCleanupArpRegistration(string installDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var statePath = InstallStatePaths.InstallStatePath(installDirectory);
            if (!File.Exists(statePath))
                return;
            var state = InstallStateIo.ReadState(installDirectory);
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsWindows()
            WindowsArpRegistration.Unregister(state);
#pragma warning restore CA1416
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private async Task<string> ShowChoiceDialogAsync(string title, string message, string defaultChoice, params string[] options)
    {
        if (options.Length == 0)
            throw new ArgumentException("At least one option is required.", nameof(options));

        var selected = defaultChoice;
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = new DockPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    buttonPanel,
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };

        foreach (var option in options)
        {
            var button = new Button { Content = option, MinWidth = 96 };
            button.Click += (_, _) =>
            {
                selected = option;
                dialog.Close();
            };
            buttonPanel.Children.Add(button);
        }

        dialog.Closed += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(selected))
                selected = defaultChoice;
        };

        await dialog.ShowDialog(this);
        return selected;
    }

    private static void FinalizeWindowsInstall(InstallManifest manifest, string dest)
    {
        var win = manifest.Build.Windows ?? new WindowsBuildOptions();
        var scope = string.IsNullOrWhiteSpace(win.InstallScope) ? "user" : win.InstallScope.Trim();
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase) && !IsWindowsAdministrator())
        {
            throw new InvalidOperationException(
                "Per-machine installs require Administrator rights for Add/Remove Programs registration. Use install_scope: user or run the installer elevated.");
        }

        InstallStateIo.WriteEmbeddedManifest(dest, manifest);

        var guidStr = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        var relativeKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{guidStr}";

        var state = new InstallStateDocument
        {
            ProductId = guidStr,
            DisplayName = manifest.Metadata.Name,
            DisplayVersion = manifest.Metadata.Version,
            Publisher = manifest.Metadata.Publisher,
            InstallLocation = dest,
            InstallScope = scope,
            RegistryUninstallKeyRelative = relativeKey,
        };

        InstallStateIo.WriteState(dest, state);

        var bundledUninstallPath = InstallStatePaths.UninstallPayloadPath(dest);
        if (!File.Exists(bundledUninstallPath))
        {
            throw new InvalidOperationException(
                $"Bundled uninstaller not found at '{bundledUninstallPath}'. Publish PolyInstall.Uninstall into stubs for this target before building installers.");
        }

        var uninstallPath = InstallStatePaths.UninstallExePath(dest);
        File.Copy(bundledUninstallPath, uninstallPath, overwrite: true);

        var estimatedKb = InstallDirectoryEstimator.EstimateKibRecursive(dest);
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsWindows() at call site
        WindowsArpRegistration.Register(state, uninstallPath, estimatedKb);
#pragma warning restore CA1416
    }

    private static bool IsWindowsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var wi = WindowsIdentity.GetCurrent();
        var wp = new WindowsPrincipal(wi);
        return wp.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
