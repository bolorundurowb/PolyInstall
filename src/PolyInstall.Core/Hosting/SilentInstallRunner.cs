using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Hosting;

/// <summary>
/// UI-less install path shared by <c>PolyInstall.Runtime.Silent</c> and the wizard stub's
/// <c>--silent</c> flag. Cancellation is driven by an injected <see cref="CancellationToken"/>
/// so unit tests do not touch process-global signal state.
/// </summary>
public static class SilentInstallRunner
{
    public static int Run(
        InstallManifest manifest,
        string extractRoot,
        IPolyInstallPal pal,
        ExistingInstallInfo? existingInstall,
        SilentInstallOptions options,
        CancellationToken cancellationToken = default,
        TextWriter? stdout = null)
    {
        stdout ??= Console.Out;

        if (options.Help)
        {
            stdout.WriteLine(SilentInstallOptions.HelpText);
            return SilentExitCodes.Success;
        }

        SilentLogSink? log = null;
        try
        {
            try
            {
                log = SilentLogSink.Open(options.LogPath, options.LogAppend);
            }
            catch (Exception ex)
            {
                stdout.WriteLine("error: " + ex.Message);
                return SilentExitCodes.InvalidArguments;
            }

            void Progress(string message)
            {
                var line = SilentInstallOptions.FormatTimestamp(DateTimeOffset.UtcNow) + " " + message;
                stdout.WriteLine(line);
                log?.WriteLine(line);
            }

            Progress("Silent install starting. Log: " + log.Path);

            // Elevation before any disk work (other than the log). Close the log so the
            // elevated child can reopen it for append.
            if (WindowsElevation.ShouldRelaunchElevated(
                    manifest,
                    existingInstall,
                    isWindows: OperatingSystem.IsWindows(),
                    isAdministrator: pal.Elevation.IsElevated))
            {
                var logPath = log.Path;
                log.Dispose();
                log = null;
                return RelaunchElevated(pal, options, logPath, msg =>
                {
                    stdout.WriteLine(msg);
                });
            }

            if (!OperatingSystem.IsWindows()
                && NeedsPosixElevation(manifest, existingInstall)
                && !pal.Elevation.IsElevated)
            {
                Progress(
                    "A system-scope service install requires root. Re-run this installer under sudo.");
                return SilentExitCodes.ElevationRequired;
            }

            InstallBootstrap.Init(manifest, extractRoot, pal, existingInstall);

            if (HasEulaStep(manifest) && !options.AcceptEula)
            {
                Progress("EULA step is present but --accept-eula was not supplied.");
                return SilentExitCodes.EulaNotAccepted;
            }

            if (!TryResolveFeatures(manifest, options, out var featureError))
            {
                Progress(featureError!);
                return SilentExitCodes.InvalidArguments;
            }

            var destination = ResolveDestination(manifest, pal, existingInstall, options);
            if (destination is null)
            {
                Progress("Could not resolve an install directory. Pass --install-dir <absolute-path>.");
                return SilentExitCodes.InvalidArguments;
            }

            DetectExistingInstallAtDestination(manifest, destination);
            existingInstall = InstallBootstrap.ExistingInstall;

            if (IsForeignProductAtDestination(manifest, destination))
            {
                Progress("An existing install of a different product was found at the destination.");
                return SilentExitCodes.ExistingInstallBlocks;
            }

            // Re-check elevation after destination-based existing-install detection.
            if (WindowsElevation.ShouldRelaunchElevated(
                    manifest,
                    existingInstall,
                    isWindows: OperatingSystem.IsWindows(),
                    isAdministrator: pal.Elevation.IsElevated))
            {
                var logPath = log.Path;
                log.Dispose();
                log = null;
                return RelaunchElevated(pal, options, logPath, msg => stdout.WriteLine(msg));
            }

            var resolvedDest = InstallPathResolver.Expand(destination, pal);
            var running = pal.Processes.FindProcessesUnderDirectory(resolvedDest);
            if (running.Count > 0)
            {
                if (!options.CloseApps)
                {
                    Progress(
                        $"{running.Count} process(es) are running from the install location. Pass --close-apps to terminate them, or close them manually.");
                    return SilentExitCodes.ProcessesRunning;
                }

                Progress("Terminating processes running from the install location…");
                var remaining = InstallProcessGuard.TerminateAndRescan(pal.Processes, running, resolvedDest);
                if (remaining.Count > 0)
                {
                    Progress($"{remaining.Count} process(es) could not be terminated.");
                    return SilentExitCodes.ProcessesRunning;
                }
            }

            var touchedDisk = false;
            var createdInstallDirectory = false;
            string? activeInstallDirectory = null;
            var activeMode = InstallBootstrap.SelectedInstallMode;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = InstallCoordinator.Run(new InstallOperationOptions
                {
                    Manifest = manifest,
                    ExtractRoot = extractRoot,
                    Destination = destination,
                    Pal = pal,
                    ExistingInstall = existingInstall,
                    CancellationToken = cancellationToken,
                    Progress = Progress,
                    OnInstallDirectoryPrepared = info =>
                    {
                        touchedDisk = true;
                        createdInstallDirectory = info.CreatedInstallDirectory;
                        activeInstallDirectory = info.InstallDirectory;
                        activeMode = info.Mode;
                    },
                });

                InstallBootstrap.InstallDirectory = result.InstallDirectory;
                InstallBootstrap.SelectedInstallMode = result.Mode;
                Progress(result.Mode switch
                {
                    InstallMode.Update => "Update complete.",
                    InstallMode.Repair => "Repair complete.",
                    _ => "Installation complete.",
                });
                return SilentExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                Progress("Setup was cancelled.");
                if (touchedDisk
                    && !string.IsNullOrWhiteSpace(activeInstallDirectory)
                    && activeMode == InstallMode.Install)
                {
                    Progress("Running cleanup for the cancelled fresh install…");
                    PartialInstallCleanup.Run(
                        activeInstallDirectory!,
                        extractRoot,
                        createdInstallDirectory);
                    Progress("Cleanup completed.");
                }

                return SilentExitCodes.Cancelled;
            }
        }
        catch (Exception ex)
        {
            stdout.WriteLine("error: " + ex.Message);
            log?.WriteLine(SilentInstallOptions.FormatTimestamp(DateTimeOffset.UtcNow) + " error: " + ex.Message);
            return SilentExitCodes.UnexpectedError;
        }
        finally
        {
            log?.Dispose();
        }
    }

    private static int RelaunchElevated(
        IPolyInstallPal pal,
        SilentInstallOptions options,
        string logPath,
        Action<string> progress)
    {
        var exe = Environment.ProcessPath
                   ?? throw new InvalidOperationException("Cannot resolve host executable path.");
        var forwarded = BuildForwardedArgs(options, logPath);
        progress("Elevation required; relaunching…");
        progress("Log will continue at: " + logPath);

        var result = pal.Elevation.RelaunchAndWait(exe, forwarded);
        return result.Status switch
        {
            ElevationStatus.Completed => result.ExitCode,
            ElevationStatus.Denied => SilentExitCodes.ElevationRequired,
            ElevationStatus.Unsupported => SilentExitCodes.ElevationRequired,
            _ => SilentExitCodes.ElevationRequired,
        };
    }

    /// <summary>
    /// Build the argument list for the elevated child. Always includes <c>--silent</c> and the
    /// resolved <c>--log</c> path so the child does not create a second log directory.
    /// </summary>
    internal static List<string> BuildForwardedArgs(SilentInstallOptions options, string logPath)
    {
        var args = new List<string> { "--silent", "--log", logPath, "--log-append" };
        if (options.AcceptEula)
            args.Add("--accept-eula");
        if (options.CloseApps)
            args.Add("--close-apps");
        if (!string.IsNullOrWhiteSpace(options.InstallDir))
        {
            args.Add("--install-dir");
            args.Add(options.InstallDir!);
        }
        if (!string.IsNullOrWhiteSpace(options.Features))
        {
            args.Add("--features");
            args.Add(options.Features!);
        }
        return args;
    }

    private static bool NeedsPosixElevation(InstallManifest manifest, ExistingInstallInfo? existing)
    {
        // Machine-scope is a Windows concept today; on POSIX, system-scope services imply root.
        if (manifest.Services is not { Count: > 0 })
            return false;
        return manifest.Services.Any(s =>
            string.Equals(s.Scope, "system", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Scope, "machine", StringComparison.OrdinalIgnoreCase))
            || existing?.State?.RegisteredServices?.Any(s =>
                s.Scope.Equals("system", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool HasEulaStep(InstallManifest manifest) =>
        manifest.Ui.WizardSteps.Any(s =>
            string.Equals(s.Type?.Trim(), "eula", StringComparison.OrdinalIgnoreCase));

    private static bool TryResolveFeatures(InstallManifest manifest, SilentInstallOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(options.Features))
            return true; // keep InstallBootstrap seed

        var defined = (manifest.Features ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Id))
            .Select(f => f.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var token = options.Features.Trim();
        if (token.Equals("default", StringComparison.OrdinalIgnoreCase))
            return true;

        if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            InstallBootstrap.SelectedFeatures = defined;
            return true;
        }

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!defined.Contains(part))
            {
                error = $"Unknown feature id '{part}'.";
                return false;
            }
            selected.Add(part);
        }

        InstallBootstrap.SelectedFeatures = selected;
        return true;
    }

    private static string? ResolveDestination(
        InstallManifest manifest,
        IPolyInstallPal pal,
        ExistingInstallInfo? existingInstall,
        SilentInstallOptions options)
    {
        if (existingInstall is not null && !string.IsNullOrWhiteSpace(existingInstall.InstallLocation))
            return existingInstall.InstallLocation;

        if (!string.IsNullOrWhiteSpace(options.InstallDir))
            return options.InstallDir;

        var destinationStep = manifest.Ui.WizardSteps
            .FirstOrDefault(s => string.Equals(s.Type?.Trim(), "destination", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(destinationStep?.DefaultPath))
            return InstallPathResolver.Expand(destinationStep!.DefaultPath!, pal);

        return DefaultInstallPathResolver.GetDefaultInstallPath(manifest, pal);
    }

    private static void DetectExistingInstallAtDestination(InstallManifest manifest, string destination)
    {
        if (InstallBootstrap.ExistingInstall is not null || string.IsNullOrWhiteSpace(destination))
            return;

        var expanded = InstallPathResolver.Expand(destination, InstallBootstrap.Pal);
        var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, expanded);
        if (existing is null)
            return;

        InstallBootstrap.ExistingInstall = existing;
        InstallBootstrap.SelectedInstallMode = InstallCoordinator.ResolveMode(manifest, existing);
        InstallBootstrap.InstallDirectory = existing.InstallLocation;
    }

    private static bool IsForeignProductAtDestination(InstallManifest manifest, string destination)
    {
        try
        {
            var expanded = InstallPathResolver.Expand(destination, InstallBootstrap.Pal);
            var statePath = InstallStatePaths.InstallStatePath(expanded);
            if (!File.Exists(statePath))
                return false;

            var state = InstallStateIo.ReadState(expanded);
            var expected = ProductIdHelper.StableProductGuidString(manifest.Metadata);
            return !state.ProductId.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
