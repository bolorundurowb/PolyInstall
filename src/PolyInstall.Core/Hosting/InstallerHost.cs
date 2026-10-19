using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
using PolyInstall.Payload;

namespace PolyInstall.Hosting;

/// <summary>
/// Shared bootstrap for installer stubs: read the appended bundle, extract the payload, and
/// optionally hand off to the silent runner. Platform elevation goes through
/// <see cref="IElevationPal"/>; quoting lives in the Windows PAL implementation.
/// </summary>
public static class InstallerHost
{
    public sealed class Session : IDisposable
    {
        public required InstallManifest Manifest { get; init; }
        public required IPolyInstallPal Pal { get; init; }
        public required ExistingInstallInfo? ExistingInstall { get; init; }
        public required string ExtractRoot { get; init; }
        public required string ZipPath { get; init; }
        public required string ExePath { get; init; }

        public void Dispose()
        {
            TryDeleteFile(ZipPath);
            TryDeleteDirectory(ExtractRoot);
        }
    }

    public readonly record struct BundleProbe(
        string ExePath,
        InstallManifest Manifest,
        IPolyInstallPal Pal,
        ExistingInstallInfo? ExistingInstall);

    /// <summary>Read the embedded manifest and locate an existing install without extracting.</summary>
    public static BundleProbe Probe()
    {
        var exe = Environment.ProcessPath
                   ?? throw new InvalidOperationException("Cannot resolve host executable path.");
        var manifest = InstallBundleReader.ReadManifestFromSeekableFile(exe);
        var pal = new DefaultPolyInstallPal();
        var existingInstall = InstalledProductLocator.Find(manifest, pal);
        return new BundleProbe(exe, manifest, pal, existingInstall);
    }

    /// <summary>Extract the payload for an already-probed bundle.</summary>
    public static Session Open(BundleProbe probe)
    {
        var extract = Path.Combine(Path.GetTempPath(), "polyinstall-" + Guid.NewGuid().ToString("n"));
        var zipPath = Path.Combine(Path.GetTempPath(), "polyinstall-payload-" + Guid.NewGuid().ToString("n") + ".zip");
        Directory.CreateDirectory(extract);
        InstallBundleReader.DecompressPayloadToFile(probe.ExePath, probe.Manifest, zipPath);
        ZipPayloadExtractor.ExtractFileToDirectory(zipPath, extract);

        return new Session
        {
            Manifest = probe.Manifest,
            Pal = probe.Pal,
            ExistingInstall = probe.ExistingInstall,
            ExtractRoot = extract,
            ZipPath = zipPath,
            ExePath = probe.ExePath,
        };
    }

    /// <summary>Probe and extract in one step (silent path; elevation is handled inside the runner).</summary>
    public static Session Open() => Open(Probe());

    /// <summary>
    /// Wizard path: fire-and-forget UAC relaunch before extraction. Returns true when the
    /// current process should exit.
    /// </summary>
    public static bool TryRelaunchElevatedForWizard(BundleProbe probe, string[] args)
    {
        if (!WindowsElevation.ShouldRelaunchElevated(
                probe.Manifest,
                probe.ExistingInstall,
                OperatingSystem.IsWindows(),
                probe.Pal.Elevation.IsElevated))
        {
            return false;
        }

        _ = probe.Pal.Elevation.Relaunch(probe.ExePath, args);
        // Denied / Failed: leave the non-elevated process (matches prior Runtime behaviour —
        // do not continue a doomed machine-scope install).
        return true;
    }

    /// <summary>
    /// Run a silent install against an already-opened session. Parses args, handles help, then
    /// delegates to <see cref="SilentInstallRunner"/>.
    /// </summary>
    public static int RunSilent(Session session, string[] args, CancellationToken cancellationToken = default)
    {
        if (!SilentInstallOptions.TryParse(args, out var options, out var parseError))
        {
            Console.Error.WriteLine("error: " + parseError);
            return SilentExitCodes.InvalidArguments;
        }

        if (options.Help)
        {
            Console.Out.WriteLine(SilentInstallOptions.HelpText);
            return SilentExitCodes.Success;
        }

        // Ensure --silent is set so BuildForwardedArgs always forwards it.
        if (!options.Silent)
        {
            options = new SilentInstallOptions
            {
                Silent = true,
                InstallDir = options.InstallDir,
                Features = options.Features,
                AcceptEula = options.AcceptEula,
                CloseApps = options.CloseApps,
                LogPath = options.LogPath,
                LogAppend = options.LogAppend,
                Help = options.Help,
            };
        }

        return SilentInstallRunner.Run(
            session.Manifest,
            session.ExtractRoot,
            session.Pal,
            session.ExistingInstall,
            options,
            cancellationToken);
    }

    public static void TryDeleteFile(string path)
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

    public static void TryDeleteDirectory(string path)
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
}
