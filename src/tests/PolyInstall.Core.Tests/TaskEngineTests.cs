using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

[Collection("Sequential")]
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
                    ["name"] = "app",
                    ["location"] = "desktop",
                    ["description"] = "App",
                    ["icon_path"] = @"C:\app\app.ico",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        c.Target.Should().Be($"C:{s}app{s}app.exe");
        var expectedName = OperatingSystem.IsWindows() ? "app.lnk" : "app";
        c.Shortcut.Should().EndWith(expectedName);
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
                    ["name"] = "b",
                    ["location"] = "desktop",
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
        var pal = new NoRegistryPal();
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
                    ["name"] = "Sim",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        c.Target.Should().Be($"C:{s}Install{s}Open Exam Suite{s}Simulator{s}app.exe");
        var expectedName = OperatingSystem.IsWindows() ? "Sim.lnk" : "Sim";
        c.Shortcut.Should().EndWith(expectedName);
    }

    [Fact]
    public void RunPhase_WhenCreateShortcutStartMenu_BuildsStartMenuPath()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = @"C:\app\app.exe",
                    ["name"] = "MyApp",
                    ["location"] = "start_menu",
                    ["subfolder"] = "MyVendor",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        var expectedName = OperatingSystem.IsWindows() ? "MyApp.lnk" : "MyApp";
        c.Shortcut.Should().EndWith($"MyVendor{Path.DirectorySeparatorChar}{expectedName}");
    }

    [Fact]
    public void RunPhase_WhenCreateShortcutNameAlreadyHasLnk_DoesNotDoubleAppend()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app.lnk",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var c = pal.ShortcutCalls[0];
        c.Shortcut.Should().EndWith("app.lnk");
        c.Shortcut.Should().NotEndWith("app.lnk.lnk");
    }

    [Fact]
    public void RunPhase_WhenWriteRegistryTask_RunsRegistryPal()
    {
        var pal = new RecordingPal();
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
                    ["value_kind"] = "string",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.RegistryCalls.Should().ContainSingle();
        var r = pal.RegistryCalls[0];
        r.KeyPath.Should().Be(@"HKCU\Software\Test");
        r.ValueName.Should().Be("x");
        r.Value.Should().Be("1");
        r.ValueKind.Should().Be("string");
    }

    [Fact]
    public void RunPhase_WhenCreateDesktopEntry_RunsDesktopEntryPal()
    {
        var s = Path.DirectorySeparatorChar;
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_desktop_entry",
                Parameters = new Dictionary<string, object?>
                {
                    ["file_name"] = "app.desktop",
                    ["name"] = "App",
                    ["exec"] = "/usr/bin/app",
                    ["icon"] = "/usr/share/icons/app.png",
                    ["comment"] = "My app",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.DesktopEntryCalls.Should().ContainSingle();
        var d = pal.DesktopEntryCalls[0];
        d.FileName.Should().Be("app.desktop");
        d.Name.Should().Be("App");
        d.Exec.Should().Be($"{s}usr{s}bin{s}app");
        d.Icon.Should().Be($"{s}usr{s}share{s}icons{s}app.png");
        d.Comment.Should().Be("My app");
    }

    [Fact]
    public void RunPhase_WhenSetPermissions_RunsFilePermissionsPal()
    {
        var s = Path.DirectorySeparatorChar;
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "set_permissions",
                Parameters = new Dictionary<string, object?>
                {
                    ["path"] = "/usr/bin/app",
                    ["mode"] = 755,
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.PermissionCalls.Should().ContainSingle();
        var p = pal.PermissionCalls[0];
        p.Path.Should().Be($"{s}usr{s}bin{s}app");
        p.Mode.Should().Be(755);
    }

    [Fact]
    public void RunPhase_WhenJsonElementParameters_Works()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
    }

    [Fact]
    public void RunPhase_WhenMachineScopeStartMenu_BuildsCommonProgramsPath()
    {
        var manifest = new InstallManifest
        {
            Build = new BuildConfiguration
            {
                Windows = new WindowsBuildOptions { InstallScope = "machine" },
            },
        };
        var pal = new RecordingPal();
        InstallBootstrap.Init(manifest, Path.GetTempPath(), pal);
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "MyApp",
                    ["location"] = "start_menu",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        var expectedName = OperatingSystem.IsWindows() ? "MyApp.lnk" : "MyApp";
        pal.ShortcutCalls[0].Shortcut.Should().EndWith(expectedName);
    }

    [Fact]
    public void RunPhase_WhenAddToPath_CallsPathPalWithDefaultScope()
    {
        var pal = new RecordingPal();
        var s = Path.DirectorySeparatorChar;
        var installDir = $"{s}opt{s}testapp";
        InstallBootstrap.Init(new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "Test", Version = "1.0" },
            Build = new BuildConfiguration { Targets = ["windows-x64"] },
        }, Path.GetTempPath(), pal);
        InstallBootstrap.InstallDirectory = installDir;

        var tasks = new[]
        {
            new InstallTask
            {
                Action = "add_to_path",
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.PathCalls.Should().ContainSingle();
        pal.PathCalls[0].Path.Should().Be(installDir);
        pal.PathCalls[0].Scope.Should().Be("user");
    }

    [Fact]
    public void RunPhase_WhenAddToPathWithExplicitPath_ExpandsPlaceholders()
    {
        var s = Path.DirectorySeparatorChar;
        var pal = new RecordingPal
        {
            AppDirBacking = $"C:{s}Program Files{s}MyApp",
        };
        InstallBootstrap.Init(new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "Test", Version = "1.0" },
            Build = new BuildConfiguration { Targets = ["windows-x64"] },
        }, Path.GetTempPath(), pal);
        InstallBootstrap.InstallDirectory = $"C:{s}Program Files{s}MyApp";

        var tasks = new[]
        {
            new InstallTask
            {
                Action = "add_to_path",
                Parameters = new Dictionary<string, object?>
                {
                    ["path"] = "{AppDir}\\bin",
                    ["scope"] = "machine",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.PathCalls.Should().ContainSingle();
        pal.PathCalls[0].Path.Should().Be($"C:{s}Program Files{s}MyApp{s}bin");
        pal.PathCalls[0].Scope.Should().Be("machine");
    }

    [Fact]
    public void RunPhase_WhenAddToPathPalNull_ThrowsPlatformNotSupportedException()
    {
        var pal = new NoRegistryPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "add_to_path",
            },
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<PlatformNotSupportedException>()
            .WithMessage("*PATH*");
    }

    [Fact]
    public void RunPhase_WhenFileAssociationInstall_CallsRegister()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" }
                }
            }
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.RegisterCalls.Should().HaveCount(1);
        pal.RegisterCalls[0].Extension.Should().Be(".oef");
        pal.RegisterCalls[0].Description.Should().Be("OEF File");
        pal.RegisterCalls[0].Command.Should().Be("open %1");
    }

    [Fact]
    public void RunPhase_WhenFileAssociationUninstall_CallsUnregister()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" }
                }
            }
        };

        TaskEngine.RunPhase(tasks, pal, isUninstall: true);

        pal.UnregisterCalls.Should().HaveCount(1);
        pal.UnregisterCalls[0].Extension.Should().Be(".oef");
    }

    [Fact]
    public void RunPhase_WhenFileAssociationWithMimeType_PassesMimeType()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" },
                    { "mime_type", "application/x-custom" }
                }
            }
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.RegisterCalls.Should().HaveCount(1);
        pal.RegisterCalls[0].MimeType.Should().Be("application/x-custom");
    }

    [Fact]
    public void RunPhase_WhenFileAssociationWithBundlePath_PassesBundlePath()
    {
        // bundle_path is a macOS .app path; on non-macOS hosts InstallPathResolver.Expand
        // rewrites the slashes to the host separator, making the assertion non-portable.
        if (!OperatingSystem.IsMacOS())
            return;

        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" },
                    { "bundle_path", "/Applications/MyApp.app" }
                }
            }
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.RegisterCalls.Should().HaveCount(1);
        pal.RegisterCalls[0].BundlePath.Should().Be("/Applications/MyApp.app");
    }

    [Fact]
    public void RunPhase_WhenFileAssociationMissingProgId_AutoGeneratesFromAppName()
    {
        var pal = new RecordingPal();
        InstallBootstrap.Init(new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "My Cool App!", Version = "1.0" },
            Build = new BuildConfiguration { Targets = ["windows-x64"] },
        }, Path.GetTempPath(), pal);

        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" }
                }
            }
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.RegisterCalls.Should().HaveCount(1);
        pal.RegisterCalls[0].ProgId.Should().Be("MyCoolApp.oef.1");
    }

    [Fact]
    public void RunPhase_WhenTaskFeatureNotSelected_SkipsTask()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Features = ["samples"],
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "simulator" });

        pal.ShortcutCalls.Should().BeEmpty();
    }

    [Fact]
    public void RunPhase_WhenTaskFeatureSelected_RunsTask()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Features = ["simulator"],
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "simulator" });

        pal.ShortcutCalls.Should().ContainSingle();
    }

    [Fact]
    public void RunPhase_WhenTaskHasNoFeatures_AlwaysRuns()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app",
                    ["location"] = "desktop",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        pal.ShortcutCalls.Should().ContainSingle();
    }

    [Fact]
    public void RunPhase_WhenFileAssociationPalNull_ThrowsPlatformNotSupportedException()
    {
        var pal = new NoRegistryPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "file_association",
                Parameters = new Dictionary<string, object?>
                {
                    { "extension", ".oef" },
                    { "description", "OEF File" },
                    { "command", "open %1" }
                }
            }
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<PlatformNotSupportedException>()
            .WithMessage("*File association tasks are not supported*");
    }

    [Fact]
    public void RunPhase_WhenCreateDesktopEntryPalNull_ThrowsPlatformNotSupportedException()
    {
        var pal = new NoRegistryPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_desktop_entry",
                Parameters = new Dictionary<string, object?>
                {
                    { "file_name", "app.desktop" },
                    { "name", "App" },
                    { "exec", "/usr/bin/app" }
                }
            }
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<PlatformNotSupportedException>()
            .WithMessage("*Desktop entry tasks are not supported*");
    }

    [Fact]
    public void RunPhase_WhenSetPermissionsPalNull_ThrowsPlatformNotSupportedException()
    {
        var pal = new NoRegistryPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "set_permissions",
                Parameters = new Dictionary<string, object?>
                {
                    { "path", "/usr/bin/app" },
                    { "mode", 755 }
                }
            }
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<PlatformNotSupportedException>()
            .WithMessage("*Permission tasks are not supported*");
    }

    [Fact]
    public void RunPhase_WhenUnsupportedLocation_ThrowsNotSupportedException()
    {
        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app.exe",
                    ["name"] = "app",
                    ["location"] = "taskbar",
                },
            },
        };

        FluentActions.Invoking(() => TaskEngine.RunPhase(tasks, pal))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unsupported shortcut location*");
    }

    [Fact]
    public void RunPhase_WhenLinuxShortcutStartMenu_BuildsApplicationsPath()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var pal = new RecordingPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "/usr/bin/app",
                    ["name"] = "app",
                    ["location"] = "start_menu",
                },
            },
        };

        TaskEngine.RunPhase(tasks, pal);

        pal.ShortcutCalls.Should().ContainSingle();
        pal.ShortcutCalls[0].Shortcut.Should().EndWith("app");
    }

    private sealed class RecordingPal : IPolyInstallPal
    {
        public string AppDirBacking { get; init; } = "";
        public string ProgramFilesBacking { get; init; } = "";
        public string UserHomeBacking { get; init; } = "";
        public string DesktopBacking { get; init; } = "";

        public string AppDir => AppDirBacking;
        public string ProgramFiles => ProgramFilesBacking;
        public string UserHome => UserHomeBacking;
        public string Desktop => DesktopBacking;

        public IShortcutPal Shortcuts { get; }
        public IRegistryPal? Registry { get; }
        public IDesktopEntryPal? DesktopEntries { get; }
        public IFilePermissionsPal? FilePermissions { get; }
        public IPathPal? Path { get; }
        public IFileAssociationPal? FileAssociations { get; }
        public IServiceManagerPal? Services => null;

        public List<(string Target, string Shortcut, string? Description, string? Icon)> ShortcutCalls { get; } = [];
        public List<(string KeyPath, string? ValueName, string Value, string ValueKind)> RegistryCalls { get; } = [];
        public List<(string FileName, string Name, string Exec, string? Icon, string? Comment)> DesktopEntryCalls { get; } = [];
        public List<(string Path, int Mode)> PermissionCalls { get; } = [];
        public List<(string Path, string Scope)> PathCalls { get; } = [];
        public List<FileAssociationInfo> RegisterCalls { get; } = [];
        public List<FileAssociationInfo> UnregisterCalls { get; } = [];

        public RecordingPal()
        {
            Shortcuts = new RecordingShortcutPal(this);
            Registry = new RecordingRegistryPal(this);
            DesktopEntries = new RecordingDesktopEntryPal(this);
            FilePermissions = new RecordingFilePermissionsPal(this);
            Path = new RecordingPathPal(this);
            FileAssociations = new RecordingFileAssociationPal(this);
        }

        private sealed class RecordingShortcutPal(RecordingPal owner) : IShortcutPal
        {
            public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
            {
                owner.ShortcutCalls.Add((targetPath, shortcutPath, description, iconPath));
            }
        }

        private sealed class RecordingRegistryPal(RecordingPal owner) : IRegistryPal
        {
            public void SetValue(string keyPath, string? valueName, string value, string valueKind)
            {
                owner.RegistryCalls.Add((keyPath, valueName, value, valueKind));
            }
        }

        private sealed class RecordingDesktopEntryPal(RecordingPal owner) : IDesktopEntryPal
        {
            public void CreateDesktopEntry(string fileName, string name, string exec, string? icon, string? comment)
            {
                owner.DesktopEntryCalls.Add((fileName, name, exec, icon, comment));
            }
        }

        private sealed class RecordingFilePermissionsPal(RecordingPal owner) : IFilePermissionsPal
        {
            public void SetFileMode(string path, int mode)
            {
                owner.PermissionCalls.Add((path, mode));
            }
        }

        private sealed class RecordingPathPal(RecordingPal owner) : IPathPal
        {
            private readonly List<(string Path, string Scope)> _addedPaths = [];

            public void AddToPath(string path, string scope)
            {
                owner.PathCalls.Add((path, scope));
                _addedPaths.Add((path, scope));
            }

            public void RemoveFromPath(string path, string scope) { }

            public IReadOnlyList<(string Path, string Scope)> AddedPaths => _addedPaths;
        }

        private sealed class RecordingFileAssociationPal(RecordingPal owner) : IFileAssociationPal
        {
            public void Register(FileAssociationInfo association) => owner.RegisterCalls.Add(association);
            public void Unregister(FileAssociationInfo association) => owner.UnregisterCalls.Add(association);
        }
    }

    private sealed class NoRegistryPal : IPolyInstallPal
    {
        public string AppDir => "";
        public string ProgramFiles => "";
        public string UserHome => "";
        public string Desktop => "";
        public IShortcutPal Shortcuts { get; } = new NullShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IPathPal? Path => null;
        public IFileAssociationPal? FileAssociations => null;
        public IServiceManagerPal? Services => null;
    }

    private sealed class NullShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
