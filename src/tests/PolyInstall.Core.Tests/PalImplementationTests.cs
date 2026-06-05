using System.Runtime.Versioning;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

public class PalImplementationTests
{
    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_WithAllParameters_BuildsCorrectScript()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\Program Files\MyApp\app.exe",
            @"C:\Users\Test\Desktop\MyApp.lnk",
            "My Application",
            @"C:\Program Files\MyApp\app.ico");

        script.Should().Contain("$w = New-Object -ComObject WScript.Shell");
        script.Should().Contain("CreateShortcut");
        script.Should().Contain("TargetPath");
        script.Should().Contain("Description");
        script.Should().Contain("IconLocation");
        script.Should().Contain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_WithMinimalParameters_BuildsCorrectScript()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\app.exe",
            @"C:\shortcut.lnk",
            null,
            null);

        script.Should().Contain("$w = New-Object -ComObject WScript.Shell");
        script.Should().Contain("CreateShortcut");
        script.Should().Contain("TargetPath");
        script.Should().NotContain("Description");
        script.Should().NotContain("IconLocation");
        script.Should().Contain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_EscapesSingleQuotes()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\O'Brien\app.exe",
            @"C:\Users\Test\Desktop\O'Brien.lnk",
            null,
            null);

        script.Should().Contain("O''Brien");
    }

    [Fact]
    public void UnixSymlinkShortcut_BuildFallbackScript_BuildsValidShellScript()
    {
        var script = UnixSymlinkShortcut.BuildFallbackScript("/usr/bin/myapp");

        script.Should().StartWith("#!/bin/sh");
        script.Should().Contain("exec");
        script.Should().Contain("/usr/bin/myapp");
        script.Should().Contain("\"$@\"");
    }

    [Fact]
    public void UnixSymlinkShortcut_BuildFallbackScript_EscapesQuotes()
    {
        var script = UnixSymlinkShortcut.BuildFallbackScript("/usr/bin/my \"app\"");

        script.Should().Contain("my \\\"app\\\"");
    }

    [Fact]
    public void UnixDesktopEntryPal_BuildDesktopEntryContent_WithAllParameters_BuildsCorrectContent()
    {
        var content = UnixDesktopEntryPal.BuildDesktopEntryContent(
            "My Application",
            "/usr/bin/myapp",
            "/usr/share/icons/myapp.png",
            "A great application");

        content.Should().Contain("[Desktop Entry]");
        content.Should().Contain("Type=Application");
        content.Should().Contain("Name=My Application");
        content.Should().Contain("Exec=/usr/bin/myapp");
        content.Should().Contain("Terminal=false");
        content.Should().Contain("Icon=/usr/share/icons/myapp.png");
        content.Should().Contain("Comment=A great application");
    }

    [Fact]
    public void UnixDesktopEntryPal_BuildDesktopEntryContent_WithMinimalParameters_BuildsCorrectContent()
    {
        var content = UnixDesktopEntryPal.BuildDesktopEntryContent(
            "MyApp",
            "/usr/bin/myapp",
            null,
            null);

        content.Should().Contain("[Desktop Entry]");
        content.Should().Contain("Type=Application");
        content.Should().Contain("Name=MyApp");
        content.Should().Contain("Exec=/usr/bin/myapp");
        content.Should().Contain("Terminal=false");
        content.Should().NotContain("Icon=");
        content.Should().NotContain("Comment=");
    }

    [Fact]
    public void UnixPathPal_SanitizeFileName_RemovesInvalidCharacters()
    {
        var sanitized = UnixPathPal.SanitizeFileName("/usr/local/bin/myapp");

        sanitized.Should().NotContain("/");
        sanitized.Should().NotContain("\\");
        sanitized.Should().NotContain(":");
    }

    [Fact]
    public void UnixPathPal_SanitizeFileName_HandlesEmptyString()
    {
        var sanitized = UnixPathPal.SanitizeFileName("");

        sanitized.Should().BeEmpty();
    }

    [Fact]
    public void UnixPathPal_SanitizeFileName_HandlesPathWithOnlySeparators()
    {
        var sanitized = UnixPathPal.SanitizeFileName("///");

        sanitized.Should().BeEmpty();
    }

    [Fact]
    public void UnixPathPal_FindShellProfile_PrefersBashrc()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var bashrc = Path.Combine(tempDir, ".bashrc");
            var zshrc = Path.Combine(tempDir, ".zshrc");
            var profile = Path.Combine(tempDir, ".profile");

            File.WriteAllText(bashrc, "");
            File.WriteAllText(zshrc, "");
            File.WriteAllText(profile, "");

            var result = UnixPathPal.FindShellProfile(tempDir);

            result.Should().Be(bashrc);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixPathPal_FindShellProfile_FallsBackToZshrc()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var zshrc = Path.Combine(tempDir, ".zshrc");
            var profile = Path.Combine(tempDir, ".profile");

            File.WriteAllText(zshrc, "");
            File.WriteAllText(profile, "");

            var result = UnixPathPal.FindShellProfile(tempDir);

            result.Should().Be(zshrc);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixPathPal_FindShellProfile_FallsBackToProfile()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var profile = Path.Combine(tempDir, ".profile");
            File.WriteAllText(profile, "");

            var result = UnixPathPal.FindShellProfile(tempDir);

            result.Should().Be(profile);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixPathPal_FindShellProfile_DefaultsToBashrc()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var result = UnixPathPal.FindShellProfile(tempDir);

            result.Should().Be(Path.Combine(tempDir, ".bashrc"));
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PathPal_AddedPaths_IsReadOnly()
    {
        var pal = new PathPal();

        var paths = pal.AddedPaths;

        paths.Should().BeAssignableTo<IReadOnlyList<(string, string)>>();
    }

    [Fact]
    public void PathPal_AddToPath_EmptyList_Initially()
    {
        var pal = new PathPal();

        pal.AddedPaths.Should().BeEmpty();
    }

    [Fact]
    public void DefaultShortcutPal_CreateFileShortcut_OnWindows_CreatesShortcut()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var targetPath = Path.Combine(tempDir, "target.exe");
            var shortcutPath = Path.Combine(tempDir, "shortcut.lnk");
            File.WriteAllText(targetPath, "");

            var pal = new DefaultShortcutPal();
            pal.CreateFileShortcut(targetPath, shortcutPath, "Test", null);

            File.Exists(shortcutPath).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void DefaultShortcutPal_CreateFileShortcut_OnUnix_CreatesSymlink()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var targetPath = Path.Combine(tempDir, "target");
            var shortcutPath = Path.Combine(tempDir, "shortcut");
            File.WriteAllText(targetPath, "");

            var pal = new DefaultShortcutPal();
            pal.CreateFileShortcut(targetPath, shortcutPath, null, null);

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixSymlinkShortcut_Create_CreatesSymlinkOrFallback()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var targetPath = Path.Combine(tempDir, "target");
            var shortcutPath = Path.Combine(tempDir, "shortcut");
            File.WriteAllText(targetPath, "");

            UnixSymlinkShortcut.Create(targetPath, shortcutPath);

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixSymlinkShortcut_Create_OverwritesExistingFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var targetPath = Path.Combine(tempDir, "target");
            var shortcutPath = Path.Combine(tempDir, "shortcut");
            File.WriteAllText(targetPath, "");
            File.WriteAllText(shortcutPath, "old content");

            UnixSymlinkShortcut.Create(targetPath, shortcutPath);

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixDesktopEntryPal_CreateDesktopEntry_WritesFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new UnixDesktopEntryPal();
            pal.CreateDesktopEntry("test", "Test App", "/usr/bin/test", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "test.desktop");
            File.Exists(expectedPath).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixDesktopEntryPal_CreateDesktopEntry_AppendsDesktopExtension()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new UnixDesktopEntryPal();
            pal.CreateDesktopEntry("myapp", "My App", "/usr/bin/myapp", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop");
            File.Exists(expectedPath).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixDesktopEntryPal_CreateDesktopEntry_DoesNotDoubleAppendExtension()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new UnixDesktopEntryPal();
            pal.CreateDesktopEntry("myapp.desktop", "My App", "/usr/bin/myapp", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop");
            File.Exists(expectedPath).Should().BeTrue();

            var doubleExtensionPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop.desktop");
            File.Exists(doubleExtensionPath).Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void UnixFilePermissionsPal_SetUnixFileMode_OnUnix_SetsPermissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "test.sh");
            File.WriteAllText(filePath, "#!/bin/sh\necho test");

            var pal = new UnixFilePermissionsPal();
            pal.SetUnixFileMode(filePath, 0b111_101_101);

            var fileInfo = new FileInfo(filePath);
            var mode = fileInfo.UnixFileMode;
            (mode & UnixFileMode.UserExecute).Should().Be(UnixFileMode.UserExecute);
            (mode & UnixFileMode.GroupExecute).Should().Be(UnixFileMode.GroupExecute);
            (mode & UnixFileMode.OtherExecute).Should().Be(UnixFileMode.OtherExecute);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsRegistryPal_SetValue_OnWindows_SetsRegistryValue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var testKeyPath = @"HKCU\Software\PolyInstallTest_" + Guid.NewGuid().ToString("N");
        try
        {
            var pal = new WindowsRegistryPal();
            pal.SetValue(testKeyPath, "TestValue", "TestData", "string");

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\" + testKeyPath.Split('\\').Last());
            key.Should().NotBeNull();
            key!.GetValue("TestValue").Should().Be("TestData");
        }
        finally
        {
            try
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\" + testKeyPath.Split('\\').Last());
            }
            catch { }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsRegistryPal_SetValue_WithInvalidKeyPath_ThrowsArgumentException()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var pal = new WindowsRegistryPal();
        Action act = () => pal.SetValue("InvalidKeyPath", "TestValue", "TestData", "string");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*HKCU\\Software*");
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsRegistryPal_SetValue_WithUnsupportedRoot_ThrowsNotSupportedException()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var pal = new WindowsRegistryPal();
        Action act = () => pal.SetValue(@"HKCR\.myext", "TestValue", "TestData", "string");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Registry root not supported*");
    }
}
