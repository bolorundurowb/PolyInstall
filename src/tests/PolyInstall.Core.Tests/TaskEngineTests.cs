using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Tests;

public class TaskEngineTests
{
    [Fact]
    public void RunPhase_InvokesCreateShortcut_WhenRequireEmpty()
    {
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
        c.Target.Should().Be(@"C:\app\app.exe");
        c.Shortcut.Should().Be(@"C:\Public\app.lnk");
        c.Description.Should().Be("App");
        c.Icon.Should().Be(@"C:\app\app.ico");
    }

    [Fact]
    public void RunPhase_SkipsTask_WhenRequireEvaluatesFalse()
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
    public void RunPhase_UnknownAction_Throws()
    {
        var pal = new RecordingPal();
        FluentActions.Invoking(() => TaskEngine.RunPhase(
                [new InstallTask { Action = "unknown_action" }],
                pal))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unknown task action*");
    }

    [Fact]
    public void RunPhase_WriteRegistry_ThrowsWhenRegistryUnsupported()
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
    public void RunPhase_NullEnumerable_IsNoOp()
    {
        var pal = new RecordingPal();
        TaskEngine.RunPhase(null, pal);
        pal.ShortcutCalls.Should().BeEmpty();
    }

    private sealed class RecordingPal : IPolyInstallPal
    {
        public IRegistryPal? RegistryBacking { get; init; } = new RecordingRegistryPal();

        public string AppDir => "";
        public string ProgramFiles => "";
        public string UserHome => "";
        public string Desktop => "";
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
