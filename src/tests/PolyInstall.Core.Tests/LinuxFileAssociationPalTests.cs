using System.Xml.Linq;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

[Collection("Sequential")]
public class LinuxFileAssociationPalTests
{
    [Fact]
    public void ResolveMimeType_WithExplicitMimeType_ReturnsIt()
    {
        var assoc = new FileAssociationInfo
        {
            Extension = ".oef",
            MimeType = "application/x-custom",
        };

        var result = LinuxFileAssociationPal.ResolveMimeType(assoc);

        result.Must().Be("application/x-custom");
    }

    [Fact]
    public void ResolveMimeType_WithoutExplicitMimeType_GeneratesFromExtension()
    {
        var assoc = new FileAssociationInfo
        {
            Extension = ".oef",
        };

        var result = LinuxFileAssociationPal.ResolveMimeType(assoc);

        result.Must().Be("application/x-oef");
    }

    [Fact]
    public void ResolveMimeType_ExtensionWithDot_IsTrimmed()
    {
        var assoc = new FileAssociationInfo
        {
            Extension = ".TEST",
        };

        var result = LinuxFileAssociationPal.ResolveMimeType(assoc);

        result.Must().Be("application/x-test");
    }

    [Fact]
    public void ResolveDesktopFileName_SanitizesProgId()
    {
        var assoc = new FileAssociationInfo
        {
            ProgId = "MyApp!@#v1.0",
        };

        var result = LinuxFileAssociationPal.ResolveDesktopFileName(assoc);

        result.Must().Be("MyAppv1.0.desktop");
    }

    [Fact]
    public void GetMimeDatabasePath_ReturnsExpectedPath()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mime");

        LinuxFileAssociationPal.GetMimeDatabasePath().Must().Be(expected);
    }

    [Fact]
    public void GetMimePackagesPath_ReturnsExpectedPath()
    {
        var expected = Path.Combine(
            LinuxFileAssociationPal.GetMimeDatabasePath(),
            "packages");

        LinuxFileAssociationPal.GetMimePackagesPath().Must().Be(expected);
    }

    [Fact]
    public void GetApplicationsPath_ReturnsExpectedPath()
    {
        var result = LinuxFileAssociationPal.GetApplicationsPath();

        result.Must().EndWith(Path.Combine(".local", "share", "applications"));
    }

    [Fact]
    public void GetCreatedFiles_WithNoExistingFiles_ReturnsEmptyList()
    {
        var assoc = new FileAssociationInfo
        {
            ProgId = "TestApp",
            Extension = ".oef",
        };

        var result = LinuxFileAssociationPal.GetCreatedFiles(assoc, "application/x-oef");

        result.Must().BeEmpty();
    }

    [Fact]
    public void Register_OnLinux_CreatesMimeXml()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Skip if required commands are not available
        if (!CommandExists("xdg-mime") || !CommandExists("update-mime-database") || !CommandExists("update-desktop-database"))
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);
            var installDir = Path.Combine(tempDir, "install");
            Directory.CreateDirectory(installDir);
            InstallBootstrap.InstallDirectory = installDir;

            var pal = new LinuxFileAssociationPal();
            var assoc = new FileAssociationInfo
            {
                Extension = ".polytest",
                Description = "PolyInstall Test File",
                ProgId = "PolyInstall.Test",
                Command = "polytest %f",
            };

            pal.Register(assoc);

            var mimePath = Path.Combine(tempDir, ".local", "share", "mime", "packages",
                "PolyInstall-Test-polytest.xml");
            File.Exists(mimePath).Must().BeTrue();

            var content = File.ReadAllText(mimePath);
            content.Must().Contain("application/x-polytest");
            content.Must().Contain("*.polytest");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
            InstallBootstrap.InstallDirectory = null;
        }
    }

    [Fact]
    public void Unregister_OnLinux_DeletesCreatedFiles()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Skip if required commands are not available
        if (!CommandExists("xdg-mime") || !CommandExists("update-mime-database") || !CommandExists("update-desktop-database"))
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);
            var installDir = Path.Combine(tempDir, "install");
            Directory.CreateDirectory(installDir);
            InstallBootstrap.InstallDirectory = installDir;

            var pal = new LinuxFileAssociationPal();
            var assoc = new FileAssociationInfo
            {
                Extension = ".polytest2",
                Description = "PolyInstall Test File 2",
                ProgId = "PolyInstall.Test2",
                Command = "polytest2 %f",
            };

            pal.Register(assoc);
            pal.Unregister(assoc);

            var mimePath = Path.Combine(tempDir, ".local", "share", "mime", "packages",
                "PolyInstall-Test2-polytest2.xml");
            File.Exists(mimePath).Must().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
            InstallBootstrap.InstallDirectory = null;
        }
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
