using Avalonia;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Install;
using PolyInstall.Core.Payload;
using PolyInstall.Runtime.Pal;
using PolyInstall.UI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve host executable path.");
        var (manifest, compressed) = InstallBundleReader.ReadFromSeekableFile(exe);
        var raw = InstallBundleReader.DecompressPayload(manifest, compressed);
        var extract = Path.Combine(Path.GetTempPath(), "polyinstall-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(extract);
        ZipPayloadExtractor.ExtractToDirectory(raw, extract);
        var pal = new DefaultPolyInstallPal();
        InstallBootstrap.Init(manifest, extract, pal);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
