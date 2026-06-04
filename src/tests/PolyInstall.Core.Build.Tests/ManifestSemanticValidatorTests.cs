using PolyInstall.Core.Build.Validation;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Build.Tests;

public class ManifestSemanticValidatorTests
{
    [Fact]
    public void Validate_UserScopeWithHklmRegistry_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "write_registry",
                    Require = "os.isWindows",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = @"HKLM\Software\MyApp",
                        ["value_name"] = "",
                        ["value"] = "test",
                        ["value_kind"] = "string",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("HKLM");
        ex.Message.Should().Contain("install_scope is 'user'");
    }

    [Fact]
    public void Validate_UserScopeWithHkcuRegistry_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "write_registry",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = @"HKCU\Software\MyApp",
                        ["value_name"] = "",
                        ["value"] = "test",
                        ["value_kind"] = "string",
                    },
                },
            ],
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_RegistryWithUnsupportedRoot_Throws()
    {
        var manifest = CreateBaseManifest("machine");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "write_registry",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = @"HKCR\.myext",
                        ["value"] = "test",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("unsupported registry root");
    }

    [Fact]
    public void Validate_RegistryWithInvalidKeyPath_Throws()
    {
        var manifest = CreateBaseManifest("machine");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "write_registry",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = "SoftwareMyApp",
                        ["value"] = "test",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("ROOT\\SubKey");
    }

    [Fact]
    public void Validate_ShortcutWithOldShortcutPath_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["shortcut_path"] = "{Desktop}/app.lnk",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("no longer accepts 'shortcut_path'");
    }

    [Fact]
    public void Validate_ShortcutMissingName_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["location"] = "desktop",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("requires parameter 'name'");
    }

    [Fact]
    public void Validate_ShortcutMissingLocation_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["name"] = "app",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("requires parameter 'location'");
    }

    [Fact]
    public void Validate_ShortcutInvalidLocation_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
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
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("'location' must be 'start_menu' or 'desktop'");
    }

    [Fact]
    public void Validate_ShortcutWithSubfolderTraversal_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["name"] = "app",
                        ["location"] = "start_menu",
                        ["subfolder"] = "../..",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("subfolder' must be a simple relative name");
    }

    [Fact]
    public void Validate_ShortcutStartMenu_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["name"] = "app",
                        ["location"] = "start_menu",
                        ["subfolder"] = "MyVendor",
                    },
                },
            ],
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_ShortcutDesktop_Passes()
    {
        var manifest = CreateBaseManifest("machine");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
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
            ],
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_ShortcutMissingTargetPath_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["name"] = "app",
                        ["location"] = "desktop",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Should().Contain("requires parameter 'target_path'");
    }

    [Fact]
    public void Validate_NoTasks_Passes()
    {
        var manifest = CreateBaseManifest("user");
        ManifestSemanticValidator.Validate(manifest);
    }

    private static InstallManifest CreateBaseManifest(string installScope)
    {
        return new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "Test", Version = "1.0.0" },
            Build = new BuildConfiguration
            {
                Targets = ["windows-x64"],
                Windows = new WindowsBuildOptions { InstallScope = installScope },
            },
        };
    }
}
