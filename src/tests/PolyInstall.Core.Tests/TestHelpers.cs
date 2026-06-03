using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public static class TestHelpers
{
    public static InstallManifest Manifest(string name, string version, string installScope = "user") =>
        new()
        {
            Metadata = new ManifestMetadata
            {
                Name = name,
                Version = version,
                Publisher = "Example",
            },
            Build = new BuildConfiguration
            {
                Windows = new WindowsBuildOptions
                {
                    InstallScope = installScope,
                    RegisterArp = false,
                },
            },
        };

    public static InstallStateDocument StateFor(
        InstallManifest manifest,
        string installRoot,
        string version,
        List<string>? payloadFiles = null)
    {
        var productId = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        return new InstallStateDocument
        {
            ProductId = productId,
            DisplayName = manifest.Metadata.Name,
            DisplayVersion = version,
            Publisher = manifest.Metadata.Publisher,
            InstallLocation = installRoot,
            InstallScope = InstallScopeHelper.GetInstallScope(manifest),
            RegistryUninstallKeyRelative = WindowsArpRegistration.RegistryKeyRelativeForProductId(productId),
            PayloadFiles = payloadFiles,
        };
    }

    public static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "polyinstall-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
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
        }
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
        }
    }
}
