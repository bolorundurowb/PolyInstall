using System.Runtime.Versioning;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

[Collection("Sequential")]
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

        script.Must().Contain("$w = New-Object -ComObject WScript.Shell");
        script.Must().Contain("CreateShortcut");
        script.Must().Contain("TargetPath");
        script.Must().Contain("Description");
        script.Must().Contain("IconLocation");
        script.Must().Contain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_WithMinimalParameters_BuildsCorrectScript()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\app.exe",
            @"C:\shortcut.lnk",
            null,
            null);

        script.Must().Contain("$w = New-Object -ComObject WScript.Shell");
        script.Must().Contain("CreateShortcut");
        script.Must().Contain("TargetPath");
        script.Contains("Description", StringComparison.Ordinal).Must().BeFalse();
        script.Contains("IconLocation", StringComparison.Ordinal).Must().BeFalse();
        script.Must().Contain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_EscapesSingleQuotes()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\O'Brien\app.exe",
            @"C:\Users\Test\Desktop\O'Brien.lnk",
            null,
            null);

        script.Must().Contain("O''Brien");
    }

    [Fact]
    public void PosixSymlinkShortcut_BuildFallbackScript_BuildsValidShellScript()
    {
        var script = PosixSymlinkShortcut.BuildFallbackScript("/usr/bin/myapp");

        script.Must().StartWith("#!/bin/sh");
        script.Must().Contain("exec");
        script.Must().Contain("/usr/bin/myapp");
        script.Must().Contain("\"$@\"");
    }

    [Fact]
    public void PosixSymlinkShortcut_BuildFallbackScript_EscapesQuotes()
    {
        var script = PosixSymlinkShortcut.BuildFallbackScript("/usr/bin/my \"app\"");

        script.Must().Contain("my \\\"app\\\"");
    }

    [Fact]
    public void LinuxDesktopEntryPal_BuildDesktopEntryContent_WithAllParameters_BuildsCorrectContent()
    {
        var content = LinuxDesktopEntryPal.BuildDesktopEntryContent(
            "My Application",
            "/usr/bin/myapp",
            "/usr/share/icons/myapp.png",
            "A great application");

        content.Must().Contain("[Desktop Entry]");
        content.Must().Contain("Type=Application");
        content.Must().Contain("Name=My Application");
        content.Must().Contain("Exec=/usr/bin/myapp");
        content.Must().Contain("Terminal=false");
        content.Must().Contain("Icon=/usr/share/icons/myapp.png");
        content.Must().Contain("Comment=A great application");
    }

    [Fact]
    public void LinuxDesktopEntryPal_BuildDesktopEntryContent_WithMinimalParameters_BuildsCorrectContent()
    {
        var content = LinuxDesktopEntryPal.BuildDesktopEntryContent(
            "MyApp",
            "/usr/bin/myapp",
            null,
            null);

        content.Must().Contain("[Desktop Entry]");
        content.Must().Contain("Type=Application");
        content.Must().Contain("Name=MyApp");
        content.Must().Contain("Exec=/usr/bin/myapp");
        content.Must().Contain("Terminal=false");
        content.Contains("Icon=", StringComparison.Ordinal).Must().BeFalse();
        content.Contains("Comment=", StringComparison.Ordinal).Must().BeFalse();
    }

    [Fact]
    public void WindowsServiceManagerPal_BuildBinPath_QuotesExecutableAndArguments()
    {
        var info = new ServiceRegistrationInfo
        {
            Executable = @"C:\Program Files\MyApp\app.exe",
            Arguments = ["--service", "value with spaces"],
        };

        var binPath = WindowsServiceManagerPal.BuildBinPath(info);

        binPath.Must().Be("\"C:\\Program Files\\MyApp\\app.exe\" --service \"value with spaces\"");
    }

    [Fact]
    public void LinuxSystemdServiceManagerPal_BuildUnitContent_IncludesServiceFields()
    {
        var info = new ServiceRegistrationInfo
        {
            Name = "myapp",
            Description = "My App",
            Scope = "user",
            Executable = "/opt/myapp/app",
            Arguments = ["--service"],
            WorkingDirectory = "/opt/myapp",
            Restart = "on-failure",
            Environment = new Dictionary<string, string> { ["MYAPP_HOME"] = "/opt/myapp" },
        };

        var content = LinuxSystemdServiceManagerPal.BuildUnitContent(info);

        content.Must().Contain("[Unit]");
        content.Must().Contain("Description=My App");
        content.Must().Contain("ExecStart=/opt/myapp/app --service");
        content.Must().Contain("WorkingDirectory=/opt/myapp");
        content.Must().Contain("Restart=on-failure");
        content.Must().Contain("Environment=\"MYAPP_HOME=/opt/myapp\"");
        content.Must().Contain("WantedBy=default.target");
    }

    [Fact]
    public void MacOsLaunchdServiceManagerPal_BuildPlistContent_IncludesLaunchdFields()
    {
        var info = new ServiceRegistrationInfo
        {
            Name = "com.example.myapp",
            Executable = "/Applications/MyApp.app/Contents/MacOS/MyApp",
            Arguments = ["--service"],
            WorkingDirectory = "/Applications/MyApp.app",
            Restart = "always",
            Environment = new Dictionary<string, string> { ["MYAPP_HOME"] = "/Applications/MyApp.app" },
        };

        var content = MacOsLaunchdServiceManagerPal.BuildPlistContent(info);

        content.Must().Contain("<key>Label</key>");
        content.Must().Contain("<string>com.example.myapp</string>");
        content.Must().Contain("<key>ProgramArguments</key>");
        content.Must().Contain("<string>--service</string>");
        content.Must().Contain("<key>WorkingDirectory</key>");
        content.Must().Contain("<key>KeepAlive</key>");
        content.Must().Contain("<key>EnvironmentVariables</key>");
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_RemovesInvalidCharacters()
    {
        var sanitized = PosixPathPal.SanitizeFileName("/usr/local/bin/myapp");

        sanitized.Contains('/', StringComparison.Ordinal).Must().BeFalse();
        sanitized.Contains('\\', StringComparison.Ordinal).Must().BeFalse();
        sanitized.Contains(':', StringComparison.Ordinal).Must().BeFalse();
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_HandlesEmptyString()
    {
        var sanitized = PosixPathPal.SanitizeFileName("");

        sanitized.Must().BeEmpty();
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_HandlesPathWithOnlySeparators()
    {
        var sanitized = PosixPathPal.SanitizeFileName("///");

        sanitized.Must().BeEmpty();
    }

    [Fact]
    public void PosixPathPal_FindShellProfile_PrefersBashrc()
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

            var result = PosixPathPal.FindShellProfile(tempDir);

            result.Must().Be(bashrc);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_FindShellProfile_FallsBackToZshrc()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var zshrc = Path.Combine(tempDir, ".zshrc");
            var profile = Path.Combine(tempDir, ".profile");

            File.WriteAllText(zshrc, "");
            File.WriteAllText(profile, "");

            var result = PosixPathPal.FindShellProfile(tempDir);

            result.Must().Be(zshrc);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_FindShellProfile_FallsBackToProfile()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var profile = Path.Combine(tempDir, ".profile");
            File.WriteAllText(profile, "");

            var result = PosixPathPal.FindShellProfile(tempDir);

            result.Must().Be(profile);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_FindShellProfile_DefaultsToBashrc()
    {
        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var result = PosixPathPal.FindShellProfile(tempDir);

            result.Must().Be(Path.Combine(tempDir, ".bashrc"));
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

        ((object)paths).Must().ToBeAssignableTo<IReadOnlyList<(string, string)>>();
    }

    [Fact]
    public void PathPal_AddToPath_EmptyList_Initially()
    {
        var pal = new PathPal();

        pal.AddedPaths.Must().BeEmpty();
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

            File.Exists(shortcutPath).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void DefaultShortcutPal_CreateFileShortcut_OnLinuxMacOS_CreatesSymlink()
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

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixSymlinkShortcut_Create_CreatesSymlinkOrFallback()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var targetPath = Path.Combine(tempDir, "target");
            var shortcutPath = Path.Combine(tempDir, "shortcut");
            File.WriteAllText(targetPath, "");

            PosixSymlinkShortcut.Create(targetPath, shortcutPath);

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixSymlinkShortcut_Create_OverwritesExistingFile()
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

            PosixSymlinkShortcut.Create(targetPath, shortcutPath);

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void LinuxDesktopEntryPal_CreateDesktopEntry_WritesFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new LinuxDesktopEntryPal();
            pal.CreateDesktopEntry("test", "Test App", "/usr/bin/test", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "test.desktop");
            File.Exists(expectedPath).Must().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void LinuxDesktopEntryPal_CreateDesktopEntry_AppendsDesktopExtension()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new LinuxDesktopEntryPal();
            pal.CreateDesktopEntry("myapp", "My App", "/usr/bin/myapp", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop");
            File.Exists(expectedPath).Must().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void LinuxDesktopEntryPal_CreateDesktopEntry_DoesNotDoubleAppendExtension()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            var pal = new LinuxDesktopEntryPal();
            pal.CreateDesktopEntry("myapp.desktop", "My App", "/usr/bin/myapp", null, null);

            var expectedPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop");
            File.Exists(expectedPath).Must().BeTrue();

            var doubleExtensionPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop.desktop");
            File.Exists(doubleExtensionPath).Must().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixFilePermissionsPal_SetFileMode_OnLinuxMacOS_SetsPermissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var tempDir = TestHelpers.NewTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "test.sh");
            File.WriteAllText(filePath, "#!/bin/sh\necho test");

            var pal = new PosixFilePermissionsPal();
            pal.SetFileMode(filePath, 0b111_101_101);

            var fileInfo = new FileInfo(filePath);
            var mode = fileInfo.UnixFileMode;
            (mode & UnixFileMode.UserExecute).Must().Be(UnixFileMode.UserExecute);
            (mode & UnixFileMode.GroupExecute).Must().Be(UnixFileMode.GroupExecute);
            (mode & UnixFileMode.OtherExecute).Must().Be(UnixFileMode.OtherExecute);
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
            key.Must().NotBeNull();
            key!.GetValue("TestValue").Must().Be("TestData");
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

        act.Throws<ArgumentException>()
            .WithMessageContaining("HKCU\\Software");
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsRegistryPal_SetValue_WithUnsupportedRoot_ThrowsNotSupportedException()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var pal = new WindowsRegistryPal();
        Action act = () => pal.SetValue(@"HKCR\.myext", "TestValue", "TestData", "string");

        act.Throws<NotSupportedException>()
            .WithMessageContaining("Registry root not supported");
    }

    [Fact]
    public void PosixPathPal_AddToPath_UserScope_AppendsToProfile()
    {
        if (OperatingSystem.IsWindows())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);
            var profile = Path.Combine(tempDir, ".bashrc");
            File.WriteAllText(profile, "# existing content\n");

            PosixPathPal.AddToPath("/usr/local/testapp/bin", "user");

            var content = File.ReadAllText(profile);
            content.Must().Contain("export PATH=\"$PATH:/usr/local/testapp/bin\"");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_AddToPath_UserScope_DuplicateEntry_IsIgnored()
    {
        if (OperatingSystem.IsWindows())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);
            var profile = Path.Combine(tempDir, ".bashrc");
            var entry = "export PATH=\"$PATH:/usr/local/testapp/bin\"";
            File.WriteAllText(profile, $"# existing content\n{entry}\n");

            PosixPathPal.AddToPath("/usr/local/testapp/bin", "user");

            var lines = File.ReadAllLines(profile);
            lines.Count(l => l.Trim() == entry).Must().Be(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_RemoveFromPath_UserScope_RemovesEntry()
    {
        if (OperatingSystem.IsWindows())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);
            var profile = Path.Combine(tempDir, ".bashrc");
            var entry = "export PATH=\"$PATH:/usr/local/testapp/bin\"";
            File.WriteAllText(profile, $"# existing content\n{entry}\n");

            PosixPathPal.RemoveFromPath("/usr/local/testapp/bin", "user");

            var content = File.ReadAllText(profile);
            content.Contains(entry, StringComparison.Ordinal).Must().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void PosixPathPal_RemoveFromPath_UserScope_NonExistentProfile_DoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
            return;

        var tempDir = TestHelpers.NewTempDir();
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("HOME", tempDir);

            Action act = () => PosixPathPal.RemoveFromPath("/usr/local/testapp/bin", "user");

            act.NotThrow();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            TestHelpers.TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_SetsPropertiesCorrectly()
    {
        var pal = new DefaultPolyInstallPal();

        pal.UserHome.Must().Be(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        pal.Desktop.Must().Be(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        pal.Shortcuts.Must().NotBeNull();
        pal.Path.Must().NotBeNull();
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_Windows_HasRegistry()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsWindows())
        {
            pal.Registry.Must().NotBeNull();
            pal.FileAssociations.Must().NotBeNull();
            pal.FileAssociations.Must().ToBeOfType<WindowsFileAssociationPal>();
        }
        else
        {
            pal.Registry.Must().BeNull();
        }
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_Linux_HasDesktopEntries()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsLinux())
        {
            pal.DesktopEntries.Must().NotBeNull();
            pal.DesktopEntries.Must().ToBeOfType<LinuxDesktopEntryPal>();
            pal.FilePermissions.Must().NotBeNull();
            pal.FilePermissions.Must().ToBeOfType<PosixFilePermissionsPal>();
            pal.FileAssociations.Must().NotBeNull();
            pal.FileAssociations.Must().ToBeOfType<LinuxFileAssociationPal>();
        }
        else
        {
            pal.DesktopEntries.Must().BeNull();
        }
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_MacOS_HasFilePermissions()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsMacOS())
        {
            pal.FilePermissions.Must().NotBeNull();
            pal.FilePermissions.Must().ToBeOfType<PosixFilePermissionsPal>();
            pal.FileAssociations.Must().NotBeNull();
            pal.FileAssociations.Must().ToBeOfType<MacOsFileAssociationPal>();
        }
        else if (!OperatingSystem.IsLinux())
        {
            pal.FilePermissions.Must().BeNull();
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WindowsFileAssociationPal_GetBackup_WhenNoState_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var original = InstallBootstrap.InstallDirectory;
        try
        {
            InstallBootstrap.InstallDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var pal = new WindowsFileAssociationPal();
            // Use reflection since GetBackup is internal, but we want to test the null-installDir path too
            var result = typeof(WindowsFileAssociationPal).GetMethod("GetBackup",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, [typeof(string)], null)!
                .Invoke(pal, [".test"]);
            result.Must().BeNull();
        }
        finally
        {
            InstallBootstrap.InstallDirectory = original;
        }
    }
}
