using System.Security.Principal;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.UI;

public partial class MainWindow : Window
{
    private int _stepIndex;
    private TextBox? _destinationBox;
    private TextBlock? _progressText;
    private ProgressBar? _progressBar;
    private readonly List<WizardStep> _steps;

    public MainWindow()
    {
        InitializeComponent();
        _steps = InstallBootstrap.Manifest.Ui.WizardSteps.Count > 0
            ? InstallBootstrap.Manifest.Ui.WizardSteps
            : DefaultSteps();
        RenderStep(0);
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
        try
        {
            var manifest = InstallBootstrap.Manifest;
            var pal = InstallBootstrap.Pal;
            var dest = InstallBootstrap.InstallDirectory;
            if (string.IsNullOrWhiteSpace(dest))
                throw new InvalidOperationException("No install directory.");
            dest = InstallPathResolver.Expand(dest.Trim(), pal);
            Directory.CreateDirectory(dest);
            TaskEngine.RunPhase(manifest.Tasks?.PreInstall, pal);
            await Task.Run(() => DirectoryCopy.CopyRecursive(InstallBootstrap.ExtractRoot, dest));
            TaskEngine.RunPhase(manifest.Tasks?.PostInstall, pal);
            if (OperatingSystem.IsWindows())
            {
                var win = manifest.Build.Windows ?? new WindowsBuildOptions();
                if (win.RegisterArp)
                    FinalizeWindowsInstall(manifest, dest);
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
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_progressText is not null)
                    _progressText.Text = "Error: " + ex.Message;
                if (_progressBar is not null)
                    _progressBar.IsIndeterminate = false;
            });
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        if (_stepIndex > 0)
            RenderStep(_stepIndex - 1);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private static void FinalizeWindowsInstall(InstallManifest manifest, string dest)
    {
        var win = manifest.Build.Windows ?? new WindowsBuildOptions();
        var scope = string.IsNullOrWhiteSpace(win.InstallScope) ? "user" : win.InstallScope.Trim();
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase) && !IsWindowsAdministrator())
        {
            throw new InvalidOperationException(
                "Per-machine installs require Administrator rights for Add/Remove Programs registration. Use install_scope: user or run the installer elevated.");
        }

        var hostExe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve host executable path.");
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

        var uninstallPath = InstallStatePaths.UninstallExePath(dest);
        File.Copy(hostExe, uninstallPath, overwrite: true);

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
