using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
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
    private TextBlock? _progressLogText;
    private ProgressBar? _progressBar;
    private RadioButton? _featuresFullRadio;
    private RadioButton? _featuresCustomRadio;
    private StackPanel? _featuresCustomPanel;
    private readonly List<(FeatureDefinition Feature, CheckBox CheckBox)> _featureCheckboxes = [];
    private readonly List<WizardStep> _steps;
    private CancellationTokenSource? _installCts;
    private bool _installInProgress;
    private bool _installTouchedDisk;
    private bool _installCreatedInstallDirectory;
    private bool _installRan;
    private string? _activeInstallDirectory;
    private InstallMode _activeInstallMode = InstallMode.Install;

    public MainWindow()
    {
        InitializeComponent();
        TrySetBrandingImage();
        var configured = InstallBootstrap.Manifest.Ui.WizardSteps.Count > 0
            ? InstallBootstrap.Manifest.Ui.WizardSteps
            : DefaultSteps();
        _steps = FilterStepsForManifest(configured);
        RenderStep(0);
    }

    private static List<WizardStep> FilterStepsForManifest(List<WizardStep> steps)
    {
        var hasFeatures = InstallBootstrap.Manifest.Features is { Count: > 0 };
        if (hasFeatures)
            return new List<WizardStep>(steps);
        return steps
            .Where(s => !string.Equals(s.Type?.Trim(), "features", StringComparison.OrdinalIgnoreCase))
            .ToList();
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
        // Once an install run has completed, Back must stay disabled — re-entering earlier
        // steps after files have been touched leads to inconsistent state.
        BackButton.IsEnabled = index > 0 && !_installRan;
        var step = _steps[index];
        StepTitle.Text = step.Title ?? step.Type;
        StepContent.Content = BuildStepUi(step);
        NextButton.Content = index == _steps.Count - 1 ? "Close" : "Next";
        // After a successful install, Cancel no longer makes sense — only Close.
        if (_installRan)
            CancelButton.IsEnabled = false;
    }

    private Control BuildStepUi(WizardStep step)
    {
        var pal = InstallBootstrap.Pal;
        return step.Type.Trim().ToLowerInvariant() switch
        {
            "welcome" => new TextBlock
            {
                Text = BuildWelcomeText(),
                TextWrapping = TextWrapping.Wrap,
            },
            "eula" => BuildEula(step),
            "destination" => BuildDestination(step, pal),
            "features" => BuildFeatures(),
            "progress" => BuildProgress(),
            "finish" => new TextBlock
            {
                Text = BuildFinishText(),
                TextWrapping = TextWrapping.Wrap,
            },
            _ => new TextBlock { Text = $"Unknown step type: {step.Type}" },
        };
    }

    private Control BuildFeatures()
    {
        _featureCheckboxes.Clear();
        var features = InstallBootstrap.Manifest.Features ?? [];
        if (features.Count == 0)
        {
            return new TextBlock
            {
                Text = "No optional features defined for this installer.",
                TextWrapping = TextWrapping.Wrap,
            };
        }

        var selected = InstallBootstrap.SelectedFeatures;
        var isUpdate = InstallBootstrap.ExistingInstall is not null;

        // On a fresh install with no overrides, "Full" reflects current selection if all
        // features are selected; otherwise the user is in Custom mode.
        var allSelected = features.All(f => string.IsNullOrWhiteSpace(f.Id) || selected.Contains(f.Id));
        var defaultToCustom = isUpdate || !allSelected;

        _featuresFullRadio = new RadioButton
        {
            Content = "Full install (all features)",
            GroupName = "FeaturesMode",
            IsChecked = !defaultToCustom,
        };
        _featuresCustomRadio = new RadioButton
        {
            Content = "Custom install (choose features)",
            GroupName = "FeaturesMode",
            IsChecked = defaultToCustom,
        };

        _featuresCustomPanel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(20, 4, 0, 0),
            IsVisible = defaultToCustom,
        };

        foreach (var feat in features)
        {
            if (string.IsNullOrWhiteSpace(feat.Id))
                continue;
            var isChecked = selected.Contains(feat.Id);
            var cb = new CheckBox
            {
                Content = !string.IsNullOrWhiteSpace(feat.Description)
                    ? $"{NameOrId(feat)} — {feat.Description}"
                    : NameOrId(feat),
                IsChecked = isChecked,
            };
            _featureCheckboxes.Add((feat, cb));
            _featuresCustomPanel.Children.Add(cb);
        }

        _featuresFullRadio.IsCheckedChanged += (_, _) =>
        {
            if (_featuresFullRadio.IsChecked == true && _featuresCustomPanel is not null)
            {
                _featuresCustomPanel.IsVisible = false;
                foreach (var (_, cb) in _featureCheckboxes)
                    cb.IsChecked = true;
            }
        };
        _featuresCustomRadio.IsCheckedChanged += (_, _) =>
        {
            if (_featuresCustomRadio.IsChecked == true && _featuresCustomPanel is not null)
                _featuresCustomPanel.IsVisible = true;
        };

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = isUpdate
                        ? "Adjust which optional features are installed. Previously installed features are pre-selected; clearing a feature removes its files on this update."
                        : "Choose which optional features to install.",
                    TextWrapping = TextWrapping.Wrap,
                },
                _featuresFullRadio,
                _featuresCustomRadio,
                _featuresCustomPanel,
            },
        };
    }

    private static string NameOrId(FeatureDefinition feat) =>
        string.IsNullOrWhiteSpace(feat.Name) ? feat.Id : feat.Name;

    private void CaptureFeatureSelections()
    {
        if (_featureCheckboxes.Count == 0)
            return;

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var useFull = _featuresFullRadio?.IsChecked == true;
        foreach (var (feat, cb) in _featureCheckboxes)
        {
            if (string.IsNullOrWhiteSpace(feat.Id))
                continue;
            if (useFull || cb.IsChecked == true)
                selected.Add(feat.Id);
        }
        InstallBootstrap.SelectedFeatures = selected;
    }

    private static string BuildWelcomeText()
    {
        var manifest = InstallBootstrap.Manifest;
        var existing = InstallBootstrap.ExistingInstall;
        if (existing is null)
            return $"Welcome to {manifest.Metadata.Name} {manifest.Metadata.Version}.";

        return InstallBootstrap.SelectedInstallMode == InstallMode.Repair
            ? $"{manifest.Metadata.Name} {manifest.Metadata.Version} is already installed. Setup will repair the existing installation at:\n{existing.InstallLocation}"
            : $"Update {manifest.Metadata.Name} from {existing.DisplayVersion} to {manifest.Metadata.Version}.\nExisting installation:\n{existing.InstallLocation}";
    }

    private static string BuildFinishText()
    {
        if (InstallBootstrap.InstallDirectory is null)
            return "Installation was not run.";

        return InstallBootstrap.SelectedInstallMode switch
        {
            InstallMode.Update => $"Updated to:\n{InstallBootstrap.InstallDirectory}",
            InstallMode.Repair => $"Repaired installation at:\n{InstallBootstrap.InstallDirectory}",
            _ => $"Installed to:\n{InstallBootstrap.InstallDirectory}",
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
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = File.ReadAllText(full),
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private Control BuildDestination(WizardStep step, IPolyInstallPal pal)
    {
        var knownExisting = InstallBootstrap.ExistingInstall;
        var def = knownExisting?.InstallLocation ?? step.DefaultPath ?? GetDefaultInstallPath(pal);
        def = InstallPathResolver.Expand(def, pal);
        _destinationBox = new TextBox
        {
            Text = def,
            PlaceholderText = "Install folder",
            IsReadOnly = knownExisting is not null,
        };
        var browseButton = new Button { Content = "Browse...", MinWidth = 96, IsEnabled = knownExisting is null };
        browseButton.Click += OnBrowseDestination;

        var destinationRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        Grid.SetColumn(_destinationBox, 0);
        Grid.SetColumn(browseButton, 1);
        destinationRow.Children.Add(_destinationBox);
        destinationRow.Children.Add(browseButton);

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = knownExisting is null
                        ? "Choose installation directory:"
                        : "An existing installation was found. Updates will be applied in place:",
                    TextWrapping = TextWrapping.Wrap,
                },
                destinationRow,
            },
        };
    }

    private async void OnBrowseDestination(object? sender, RoutedEventArgs e)
    {
        if (_destinationBox is null)
            return;

        var currentDestination = (_destinationBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(currentDestination))
            currentDestination = GetDefaultInstallPath(InstallBootstrap.Pal);
        else
            currentDestination = InstallPathResolver.Expand(currentDestination, InstallBootstrap.Pal);

        var options = new FolderPickerOpenOptions
        {
            Title = "Choose installation directory",
            AllowMultiple = false,
        };
        var startDirectory = GetExistingDirectoryForPicker(currentDestination);
        if (startDirectory is not null)
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(startDirectory));

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        var selected = folders.FirstOrDefault();
        if (selected is null)
            return;

        _destinationBox.Text = selected.Path.IsFile ? selected.Path.LocalPath : selected.Path.ToString();
    }

    private static string? GetExistingDirectoryForPicker(string path)
    {
        try
        {
            var candidate = Path.GetFullPath(path);
            if (Directory.Exists(candidate))
                return candidate;

            candidate = Path.GetDirectoryName(candidate) ?? string.Empty;
            while (!string.IsNullOrWhiteSpace(candidate))
            {
                if (Directory.Exists(candidate))
                    return candidate;

                var parent = Directory.GetParent(candidate);
                if (parent is null)
                    return null;
                candidate = parent.FullName;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string GetDefaultInstallPath(IPolyInstallPal pal)
        => DefaultInstallPathResolver.GetDefaultInstallPath(InstallBootstrap.Manifest, pal);

    private Control BuildProgress()
    {
        _progressText = new TextBlock { Text = "Preparing…", TextWrapping = TextWrapping.Wrap };
        _progressBar = new ProgressBar { IsIndeterminate = true, Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        _progressLogText = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
        };
        var progressLogViewer = new ScrollViewer
        {
            Height = 170,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _progressLogText,
        };
        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                _progressText,
                _progressBar,
                progressLogViewer,
            },
        };
    }

    private void QueueProgressEntry(string entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_progressText is not null)
                _progressText.Text = entry;
            AppendProgressEntry(entry);
        });
    }

    private async Task AddProgressEntryAsync(string entry)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_progressText is not null)
                _progressText.Text = entry;
            AppendProgressEntry(entry);
        });
    }

    private void AppendProgressEntry(string entry)
    {
        if (_progressLogText is null)
            return;

        _progressLogText.Text = string.IsNullOrEmpty(_progressLogText.Text)
            ? entry
            : _progressLogText.Text + Environment.NewLine + entry;
    }

    private async void OnNext(object? sender, RoutedEventArgs e)
    {
        var step = _steps[_stepIndex];
        if (step.Type.Equals("destination", StringComparison.OrdinalIgnoreCase) && _destinationBox is { } destBox)
        {
            var destination = (destBox.Text ?? string.Empty).Trim();
            InstallBootstrap.InstallDirectory = destination;
            DetectExistingInstallAtSelectedDestination(destination);
        }
        else if (step.Type.Equals("features", StringComparison.OrdinalIgnoreCase))
        {
            CaptureFeatureSelections();
        }

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

    private static void DetectExistingInstallAtSelectedDestination(string destination)
    {
        if (InstallBootstrap.ExistingInstall is not null || string.IsNullOrWhiteSpace(destination))
            return;

        var expanded = InstallPathResolver.Expand(destination, InstallBootstrap.Pal);
        var existing = InstalledProductLocator.TryReadFromInstallDirectory(InstallBootstrap.Manifest, expanded);
        if (existing is null)
            return;

        InstallBootstrap.ExistingInstall = existing;
        InstallBootstrap.SelectedInstallMode = InstallCoordinator.ResolveMode(InstallBootstrap.Manifest, existing);
        InstallBootstrap.InstallDirectory = existing.InstallLocation;
    }

    private async Task RunInstallAsync()
    {
        _installCts = new CancellationTokenSource();
        _installInProgress = true;
        _installTouchedDisk = false;
        _installCreatedInstallDirectory = false;
        _activeInstallDirectory = null;
        _activeInstallMode = InstallBootstrap.SelectedInstallMode;
        BackButton.IsEnabled = false;
        NextButton.IsEnabled = false;

        try
        {
            var manifest = InstallBootstrap.Manifest;
            var pal = InstallBootstrap.Pal;
            var dest = InstallBootstrap.InstallDirectory;
            if (string.IsNullOrWhiteSpace(dest))
                throw new InvalidOperationException("No install directory.");
            var result = await Task.Run(
                () => InstallCoordinator.Run(new InstallOperationOptions
                {
                    Manifest = manifest,
                    ExtractRoot = InstallBootstrap.ExtractRoot,
                    Destination = dest,
                    Pal = pal,
                    ExistingInstall = InstallBootstrap.ExistingInstall,
                    CancellationToken = _installCts.Token,
                    Progress = QueueProgressEntry,
                    OnInstallDirectoryPrepared = info =>
                    {
                        _installTouchedDisk = true;
                        _installCreatedInstallDirectory = info.CreatedInstallDirectory;
                        _activeInstallDirectory = info.InstallDirectory;
                        _activeInstallMode = info.Mode;
                    },
                }),
                _installCts.Token);

            InstallBootstrap.InstallDirectory = result.InstallDirectory;
            InstallBootstrap.SelectedInstallMode = result.Mode;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_progressText is not null)
                    _progressText.Text = result.Mode switch
                    {
                        InstallMode.Update => "Update complete.",
                        InstallMode.Repair => "Repair complete.",
                        _ => "Installation complete.",
                    };
                AppendProgressEntry("Completed");
                if (_progressBar is not null)
                {
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = 100;
                }
                _installRan = true;
                NextButton.IsEnabled = true;
                BackButton.IsEnabled = false;
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
                AppendProgressEntry("Error: " + ex.Message);
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
        AppendProgressEntry("Cancelling");
        if (_progressBar is not null)
            _progressBar.IsIndeterminate = true;
        CancelButton.IsEnabled = false;
        _installCts?.Cancel();
    }

    private async Task HandleInstallCancellationAsync()
    {
        if (_progressText is not null)
            _progressText.Text = "Setup was cancelled.";
        AppendProgressEntry("Setup was cancelled");
        if (_progressBar is not null)
            _progressBar.IsIndeterminate = false;

        if (!_installTouchedDisk || string.IsNullOrWhiteSpace(_activeInstallDirectory))
        {
            Close();
            return;
        }

        if (_activeInstallMode != InstallMode.Install)
        {
            await ShowChoiceDialogAsync(
                "Setup cancelled",
                "Setup was cancelled. The existing installation was left in place. Run this installer again to repair or complete the update.",
                defaultChoice: "Close",
                "Close");
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
        AppendProgressEntry("Run cleanup");
        await Task.Run(() => CleanupPartialInstall(_activeInstallDirectory!));
        AppendProgressEntry("Cleanup completed");

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

}
