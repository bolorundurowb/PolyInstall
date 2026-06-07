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

        script.Verify().ToContain("$w = New-Object -ComObject WScript.Shell");
        script.Verify().ToContain("CreateShortcut");
        script.Verify().ToContain("TargetPath");
        script.Verify().ToContain("Description");
        script.Verify().ToContain("IconLocation");
        script.Verify().ToContain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_WithMinimalParameters_BuildsCorrectScript()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\app.exe",
            @"C:\shortcut.lnk",
            null,
            null);

        script.Verify().ToContain("$w = New-Object -ComObject WScript.Shell");
        script.Verify().ToContain("CreateShortcut");
        script.Verify().ToContain("TargetPath");
        script.Contains("Description", StringComparison.Ordinal).Verify().ToBeFalse();
        script.Contains("IconLocation", StringComparison.Ordinal).Verify().ToBeFalse();
        script.Verify().ToContain("Save()");
    }

    [Fact]
    public void WindowsShortcut_BuildPowerShellScript_EscapesSingleQuotes()
    {
        var script = WindowsShortcut.BuildPowerShellScript(
            @"C:\O'Brien\app.exe",
            @"C:\Users\Test\Desktop\O'Brien.lnk",
            null,
            null);

        script.Verify().ToContain("O''Brien");
    }

    [Fact]
    public void PosixSymlinkShortcut_BuildFallbackScript_BuildsValidShellScript()
    {
        var script = PosixSymlinkShortcut.BuildFallbackScript("/usr/bin/myapp");

        script.Verify().ToStartWith("#!/bin/sh");
        script.Verify().ToContain("exec");
        script.Verify().ToContain("/usr/bin/myapp");
        script.Verify().ToContain("\"$@\"");
    }

    [Fact]
    public void PosixSymlinkShortcut_BuildFallbackScript_EscapesQuotes()
    {
        var script = PosixSymlinkShortcut.BuildFallbackScript("/usr/bin/my \"app\"");

        script.Verify().ToContain("my \\\"app\\\"");
    }

    [Fact]
    public void LinuxDesktopEntryPal_BuildDesktopEntryContent_WithAllParameters_BuildsCorrectContent()
    {
        var content = LinuxDesktopEntryPal.BuildDesktopEntryContent(
            "My Application",
            "/usr/bin/myapp",
            "/usr/share/icons/myapp.png",
            "A great application");

        content.Verify().ToContain("[Desktop Entry]");
        content.Verify().ToContain("Type=Application");
        content.Verify().ToContain("Name=My Application");
        content.Verify().ToContain("Exec=/usr/bin/myapp");
        content.Verify().ToContain("Terminal=false");
        content.Verify().ToContain("Icon=/usr/share/icons/myapp.png");
        content.Verify().ToContain("Comment=A great application");
    }

    [Fact]
    public void LinuxDesktopEntryPal_BuildDesktopEntryContent_WithMinimalParameters_BuildsCorrectContent()
    {
        var content = LinuxDesktopEntryPal.BuildDesktopEntryContent(
            "MyApp",
            "/usr/bin/myapp",
            null,
            null);

        content.Verify().ToContain("[Desktop Entry]");
        content.Verify().ToContain("Type=Application");
        content.Verify().ToContain("Name=MyApp");
        content.Verify().ToContain("Exec=/usr/bin/myapp");
        content.Verify().ToContain("Terminal=false");
        content.Contains("Icon=", StringComparison.Ordinal).Verify().ToBeFalse();
        content.Contains("Comment=", StringComparison.Ordinal).Verify().ToBeFalse();
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

        binPath.Verify().ToBe("\"C:\\Program Files\\MyApp\\app.exe\" --service \"value with spaces\"");
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

        content.Verify().ToContain("[Unit]");
        content.Verify().ToContain("Description=My App");
        content.Verify().ToContain("ExecStart=/opt/myapp/app --service");
        content.Verify().ToContain("WorkingDirectory=/opt/myapp");
        content.Verify().ToContain("Restart=on-failure");
        content.Verify().ToContain("Environment=\"MYAPP_HOME=/opt/myapp\"");
        content.Verify().ToContain("WantedBy=default.target");
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

        content.Verify().ToContain("<key>Label</key>");
        content.Verify().ToContain("<string>com.example.myapp</string>");
        content.Verify().ToContain("<key>ProgramArguments</key>");
        content.Verify().ToContain("<string>--service</string>");
        content.Verify().ToContain("<key>WorkingDirectory</key>");
        content.Verify().ToContain("<key>KeepAlive</key>");
        content.Verify().ToContain("<key>EnvironmentVariables</key>");
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_RemovesInvalidCharacters()
    {
        var sanitized = PosixPathPal.SanitizeFileName("/usr/local/bin/myapp");

        sanitized.Contains('/', StringComparison.Ordinal).Verify().ToBeFalse();
        sanitized.Contains('\\', StringComparison.Ordinal).Verify().ToBeFalse();
        sanitized.Contains(':', StringComparison.Ordinal).Verify().ToBeFalse();
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_HandlesEmptyString()
    {
        var sanitized = PosixPathPal.SanitizeFileName("");

        sanitized.Verify().ToBeEmpty();
    }

    [Fact]
    public void PosixPathPal_SanitizeFileName_HandlesPathWithOnlySeparators()
    {
        var sanitized = PosixPathPal.SanitizeFileName("///");

        sanitized.Verify().ToBeEmpty();
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

            result.Verify().ToBe(bashrc);
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

            result.Verify().ToBe(zshrc);
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

            result.Verify().ToBe(profile);
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

            result.Verify().ToBe(Path.Combine(tempDir, ".bashrc"));
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

        ((object)paths).Verify().ToBeAssignableTo<IReadOnlyList<(string, string)>>();
    }

    [Fact]
    public void PathPal_AddToPath_EmptyList_Initially()
    {
        var pal = new PathPal();

        pal.AddedPaths.Verify().ToBeEmpty();
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

            File.Exists(shortcutPath).Verify().ToBeTrue();
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

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Verify().ToBeTrue();
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

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Verify().ToBeTrue();
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

            (File.Exists(shortcutPath) || Directory.Exists(shortcutPath)).Verify().ToBeTrue();
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
            File.Exists(expectedPath).Verify().ToBeTrue();
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
            File.Exists(expectedPath).Verify().ToBeTrue();
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
            File.Exists(expectedPath).Verify().ToBeTrue();

            var doubleExtensionPath = Path.Combine(tempDir, ".local", "share", "applications", "myapp.desktop.desktop");
            File.Exists(doubleExtensionPath).Verify().ToBeFalse();
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
            (mode & UnixFileMode.UserExecute).Verify().ToBe(UnixFileMode.UserExecute);
            (mode & UnixFileMode.GroupExecute).Verify().ToBe(UnixFileMode.GroupExecute);
            (mode & UnixFileMode.OtherExecute).Verify().ToBe(UnixFileMode.OtherExecute);
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
            key.Verify().NotToBeNull();
            key!.GetValue("TestValue").Verify().ToBe("TestData");
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
            content.Verify().ToContain("export PATH=\"$PATH:/usr/local/testapp/bin\"");
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
            lines.Count(l => l.Trim() == entry).Verify().ToBe(1);
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
            content.Contains(entry, StringComparison.Ordinal).Verify().ToBeFalse();
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

        pal.UserHome.Verify().ToBe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        pal.Desktop.Verify().ToBe(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        pal.Shortcuts.Verify().NotToBeNull();
        pal.Path.Verify().NotToBeNull();
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_Windows_HasRegistry()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsWindows())
        {
            pal.Registry.Verify().NotToBeNull();
            pal.FileAssociations.Verify().NotToBeNull();
            pal.FileAssociations.Verify().ToBeOfType<WindowsFileAssociationPal>();
        }
        else
        {
            pal.Registry.Verify().ToBeNull();
        }
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_Linux_HasDesktopEntries()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsLinux())
        {
            pal.DesktopEntries.Verify().NotToBeNull();
            pal.DesktopEntries.Verify().ToBeOfType<LinuxDesktopEntryPal>();
            pal.FilePermissions.Verify().NotToBeNull();
            pal.FilePermissions.Verify().ToBeOfType<PosixFilePermissionsPal>();
            pal.FileAssociations.Verify().NotToBeNull();
            pal.FileAssociations.Verify().ToBeOfType<LinuxFileAssociationPal>();
        }
        else
        {
            pal.DesktopEntries.Verify().ToBeNull();
        }
    }

    [Fact]
    public void DefaultPolyInstallPal_Constructor_MacOS_HasFilePermissions()
    {
        var pal = new DefaultPolyInstallPal();

        if (OperatingSystem.IsMacOS())
        {
            pal.FilePermissions.Verify().NotToBeNull();
            pal.FilePermissions.Verify().ToBeOfType<PosixFilePermissionsPal>();
            pal.FileAssociations.Verify().NotToBeNull();
            pal.FileAssociations.Verify().ToBeOfType<MacOsFileAssociationPal>();
        }
        else if (!OperatingSystem.IsLinux())
        {
            pal.FilePermissions.Verify().ToBeNull();
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
            result.Verify().ToBeNull();
        }
        finally
        {
            InstallBootstrap.InstallDirectory = original;
        }
    }
}
