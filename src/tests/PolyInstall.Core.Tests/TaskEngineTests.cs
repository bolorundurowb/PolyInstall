using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

public class TaskEngineTests
{
    [Fact]
    public void RunPhase_WhenRequireEmpty_CreatesShortcut()
    {
        var s = Path.DirectorySeparatorChar;
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = " create_shortcut ",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = @"C:\app\app.exe",
                    ["shortcut_path"] = @"C:\Public\app.lnk",
                    ["description"] = "App",
                    ["icon_path"] = @"C:\app\app.ico",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        c.Target.Should().Be($"C:{s}app{s}app.exe");
        c.Shortcut.Should().Be($"C:{s}Public{s}app.lnk");
        c.Description.Should().Be("App");
        c.Icon.Should().Be($"C:{s}app{s}app.ico");
    }

    [Fact]
    public void RunPhase_WhenRequireFalse_SkipsTask()
    {
        var pal = new RecordingPal();
        var require = OperatingSystem.IsWindows() ? "os.isLinux" : "os.isWindows";
        var tasks = new[]
        {
            new InstallTask
            {
                Require = require,
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "a",
                    ["shortcut_path"] = "b",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);
        pal.ShortcutCalls.Should().BeEmpty();
    }

    [Fact]
    public void RunPhase_WhenActionUnknown_ThrowsNotSupportedException()
    {
        var pal = new RecordingPal();
        FluentActions.Invoking(() => TaskEngine.RunPhase(
                [new InstallTask { Action = "unknown_action" }],
                pal))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unknown task action*");
    }

    [Fact]
    public void RunPhase_WhenRegistryUnsupported_ThrowsPlatformNotSupportedException()
    {
        var pal = new RecordingPal { RegistryBacking = null };
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "write_registry",
                Parameters = new Dictionary<string, object?>
                {
                    ["key_path"] = @"HKCU\Software\Test",
                    ["value_name"] = "x",
                    ["value"] = "1",
                    ["value_kind"] = "String",
                },
            },
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<PlatformNotSupportedException>();
    }

    [Fact]
    public void RunPhase_WhenTasksNull_DoesNothing()
    {
        var pal = new RecordingPal();
        TaskEngine.RunPhase(null, pal);
        pal.ShortcutCalls.Should().BeEmpty();
    }

    [Fact]
    public void RunPhase_WhenCreateShortcutContainsPlaceholders_ExpandsPaths()
    {
        var s = Path.DirectorySeparatorChar;
        var pal = new RecordingPal
        {
            AppDirBacking = $"C:{s}Install{s}Open Exam Suite",
            DesktopBacking = $"C:{s}Users{s}Test{s}Desktop",
        };
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = @"{AppDir}\Simulator\app.exe",
                    ["shortcut_path"] = @"{Desktop}\Sim.lnk",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        c.Target.Should().Be($"C:{s}Install{s}Open Exam Suite{s}Simulator{s}app.exe");
        c.Shortcut.Should().Be($"C:{s}Users{s}Test{s}Desktop{s}Sim.lnk");
    }

    private sealed class RecordingPal : IPolyInstallPal
    {
        public IRegistryPal? RegistryBacking { get; init; } = new RecordingRegistryPal();

        public string AppDirBacking { get; init; } = "";
        public string ProgramFilesBacking { get; init; } = "";
        public string UserHomeBacking { get; init; } = "";
        public string DesktopBacking { get; init; } = "";

        public string AppDir => AppDirBacking;
        public string ProgramFiles => ProgramFilesBacking;
        public string UserHome => UserHomeBacking;
        public string Desktop => DesktopBacking;

        public IShortcutPal Shortcuts { get; }
        public IRegistryPal? Registry => RegistryBacking;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;

        public List<(string Target, string Shortcut, string? Description, string? Icon)> ShortcutCalls { get; } = [];

        public RecordingPal()
        {
            Shortcuts = new RecordingShortcutPal(this);
        }

        private sealed class RecordingShortcutPal(RecordingPal owner) : IShortcutPal
        {
            public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
            {
                owner.ShortcutCalls.Add((targetPath, shortcutPath, description, iconPath));
            }
        }

        private sealed class RecordingRegistryPal : IRegistryPal
        {
            public void SetValue(string keyPath, string? valueName, string value, string valueKind)
            {
            }
        }
    }
}
