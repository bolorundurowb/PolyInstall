using PolyInstall.Core.Build.Validation;
using PolyInstall.Manifest;
using Assert = Xunit.Assert;

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
        ex.Message.Verify().ToContain("HKLM");
        ex.Message.Verify().ToContain("install_scope is 'user'");
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
                    Require = "os.isWindows",
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
                    Require = "os.isWindows",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = @"HKCR\.myext",
                        ["value"] = "test",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("unsupported registry root");
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
                    Require = "os.isWindows",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = "SoftwareMyApp",
                        ["value"] = "test",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("ROOT\\SubKey");
    }

    [Fact]
    public void Validate_RegistryWithoutOsPredicate_Throws()
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
                        ["key_path"] = @"HKCU\Software\MyApp",
                        ["value"] = "test",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("Windows-only");
        ex.Message.Verify().ToContain("require");
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
        ex.Message.Verify().ToContain("no longer accepts 'shortcut_path'");
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
        ex.Message.Verify().ToContain("requires parameter 'name'");
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
        ex.Message.Verify().ToContain("requires parameter 'location'");
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
        ex.Message.Verify().ToContain("'location' must be 'start_menu' or 'desktop'");
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
        ex.Message.Verify().ToContain("subfolder' must be a simple relative name");
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
        ex.Message.Verify().ToContain("requires parameter 'target_path'");
    }

    [Fact]
    public void Validate_NoTasks_Passes()
    {
        var manifest = CreateBaseManifest("user");
        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_EmptyFiles_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Files = [];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("files must contain at least one entry");
    }

    [Fact]
    public void Validate_EmptyTargets_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = [];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.targets must contain at least one target");
    }

    [Fact]
    public void Validate_UnknownTargetToken_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["win64"];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("unknown token 'win64'");
    }

    [Fact]
    public void Validate_InvalidCompression_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Compression = "zip";

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.compression must be 'brotli' or 'gzip'");
    }

    [Fact]
    public void Validate_OmittedSigning_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing.Verify().ToBeNull();

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_WindowsSigningWithCertificatePath_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions
            {
                CertificatePath = "certs/app.pfx",
                CertificatePasswordEnv = "WINDOWS_CERT_PASSWORD",
            },
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_WindowsSigningWithoutIdentity_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions(),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.signing.windows requires one signing identity source");
    }

    [Fact]
    public void Validate_WindowsSigningWithPlaintextPassword_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions
            {
                CertificatePath = "certs/app.pfx",
                CertificatePassword = "secret",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("plaintext secret");
        ex.Message.Verify().ToContain("certificate_password_env");
    }

    [Fact]
    public void Validate_WindowsSigningWithMultipleIdentitySources_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions
            {
                CertificatePath = "certs/app.pfx",
                CertificateThumbprint = "abcdef",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("must specify only one signing identity source");
    }

    [Fact]
    public void Validate_WindowsSigningWithoutWindowsTarget_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["osx-arm64"];
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions
            {
                CertificateSubject = "Example Corp",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.signing.windows is configured");
        ex.Message.Verify().ToContain("does not contain a Windows target");
    }

    [Fact]
    public void Validate_WindowsSigningWithInvalidStoreLocationAndDigest_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Windows = new WindowsSigningOptions
            {
                CertificateThumbprint = "abcdef",
                StoreLocation = "machine",
                FileDigestAlgorithm = "md5",
                TimestampDigestAlgorithm = "md5",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("store_location must be 'current_user' or 'local_machine'");
        ex.Message.Verify().ToContain("file_digest_algorithm");
        ex.Message.Verify().ToContain("timestamp_digest_algorithm");
    }

    [Fact]
    public void Validate_LinuxSigningWhenConfigured_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["linux-x64"];
        manifest.Build.Signing = new SigningBuildOptions
        {
            Linux = new LinuxSigningOptions(),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.signing.linux is not supported");
    }

    [Fact]
    public void Validate_MacOsSigningWithIdentityAndDmgNotarization_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["osx-arm64"];
        manifest.Build.Macos = new MacOsBuildOptions { Package = "dmg" };
        manifest.Build.Signing = new SigningBuildOptions
        {
            Macos = new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
                NotarizationProfile = "polyinstall-notary",
            },
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_MacOsSigningWithoutIdentity_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["osx-arm64"];
        manifest.Build.Signing = new SigningBuildOptions
        {
            Macos = new MacOsSigningOptions(),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.signing.macos.identity is required");
    }

    [Fact]
    public void Validate_MacOsSigningWithoutMacOsTarget_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Signing = new SigningBuildOptions
        {
            Macos = new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("build.signing.macos is configured");
        ex.Message.Verify().ToContain("does not contain a macOS target");
    }

    [Fact]
    public void Validate_MacOsNotarizationWithoutDmgPackage_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["osx-arm64"];
        manifest.Build.Signing = new SigningBuildOptions
        {
            Macos = new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
                NotarizationProfile = "polyinstall-notary",
            },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("notarization_profile requires build.macos.package: dmg");
    }

    [Fact]
    public void Validate_FilesAbsoluteSourceDir_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Files =
        [
            new FilesEntry { SourceDir = Path.GetTempPath(), Include = ["*.txt"] },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("files[0].source_dir must be a relative path");
    }

    [Fact]
    public void Validate_FilesSourceDirWithTraversal_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Files =
        [
            new FilesEntry { SourceDir = "../..", Include = ["*.txt"] },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("files[0].source_dir must not contain '..'");
    }

    [Fact]
    public void Validate_WizardProgressWithoutDestination_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "finish" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("'progress' step but no 'destination' step");
    }

    [Fact]
    public void Validate_WizardDestinationAfterProgress_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "destination" },
            new WizardStep { Type = "finish" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("'destination' after 'progress'");
    }

    [Fact]
    public void Validate_WizardDestinationBeforeProgress_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "destination" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "finish" },
        ];

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_DesktopEntryWithoutOsPredicate_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_desktop_entry",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["file_name"] = "app.desktop",
                        ["name"] = "App",
                        ["exec"] = "app",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("Linux-only");
    }

    [Fact]
    public void Validate_SetPermissionsWithoutOsPredicate_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "set_permissions",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["path"] = "app",
                        ["mode"] = 755,
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("Linux/macOS-only");
    }

    [Fact]
    public void Validate_AddToPathUserScope_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "add_to_path",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["scope"] = "user",
                    },
                },
            ],
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_AddToPathMachineScopeWithMachineInstall_Passes()
    {
        var manifest = CreateBaseManifest("machine");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "add_to_path",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["scope"] = "machine",
                    },
                },
            ],
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_AddToPathMachineScopeWithUserInstall_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "add_to_path",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["scope"] = "machine",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("add_to_path");
        ex.Message.Verify().ToContain("machine");
        ex.Message.Verify().ToContain("install_scope");
    }

    [Fact]
    public void Validate_AddToPathInvalidScope_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "add_to_path",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["scope"] = "global",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("'scope' must be 'user' or 'machine'");
    }

    [Fact]
    public void Validate_FileAssociationWithoutOsPredicate_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", ".oef" },
                        { "description", "OEF File" },
                        { "command", "\"app.exe\" \"%1\"" }
                    }
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("requires an OS predicate");
    }

    [Fact]
    public void Validate_FileAssociationMissingParameters_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_windows",
                    Parameters = []
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("requires parameter 'extension'");
    }

    [Fact]
    public void Validate_FileAssociationInvalidExtension_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_windows",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", "oef" },
                        { "description", "OEF File" },
                        { "command", "\"app.exe\" \"%1\"" }
                    }
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("must start with a dot");
    }

    [Fact]
    public void Validate_FileAssociationValid_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_windows",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", ".oef" },
                        { "description", "OEF File" },
                        { "command", "\"app.exe\" \"%1\"" }
                    }
                }
            ]
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_FileAssociationLinux_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_linux",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", ".oef" },
                        { "description", "OEF File" },
                        { "command", "app %1" }
                    }
                }
            ]
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_FileAssociationMacOSWithoutBundlePath_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_macos",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", ".oef" },
                        { "description", "OEF File" },
                        { "command", "open %1" }
                    }
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("bundle_path");
    }

    [Fact]
    public void Validate_FileAssociationMacOSWithBundlePath_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall = [
                new()
                {
                    Action = "file_association",
                    Require = "os.is_macos",
                    Parameters = new Dictionary<string, object?>
                    {
                        { "extension", ".oef" },
                        { "description", "OEF File" },
                        { "command", "open %1" },
                        { "bundle_path", "/Applications/MyApp.app" }
                    }
                }
            ]
        };

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_ServiceWithoutOsPredicate_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "com.example.app",
                Executable = "{AppDir}/app",
                Scope = "user",
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("services require an OS predicate");
    }

    [Fact]
    public void Validate_WindowsUserService_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "ExampleService",
                Require = "os.isWindows",
                Executable = "{AppDir}\\app.exe",
                Scope = "user",
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("Windows services support only scope 'system'");
    }

    [Fact]
    public void Validate_MacOsServiceWithUnsupportedRestart_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "com.example.app",
                Require = "os.isMacOS",
                Executable = "{AppDir}/app",
                Scope = "user",
                Restart = "on-success",
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("cannot be mapped to launchd");
    }

    [Fact]
    public void Validate_LinuxUserService_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "com.example.app",
                Require = "os.isLinux",
                Executable = "{AppDir}/app",
                Scope = "user",
                Restart = "on-failure",
            },
        ];

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_MacOsUserService_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "com.example.app",
                Require = "os.isMacOS",
                Executable = "{AppDir}/app",
                Scope = "user",
                Restart = "always",
            },
        ];

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_EmptyMetadata_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Metadata.Name = "";
        manifest.Metadata.Version = "";

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("metadata.name");
        ex.Message.Verify().ToContain("metadata.version");
    }

    [Fact]
    public void Validate_TopLevelFileAssociationWithoutDotExtension_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.FileAssociations =
        [
            new FileAssociation
            {
                Extension = "oef",
                Description = "OEF File",
                Command = "\"app.exe\" \"%1\"",
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("file_associations[0].extension");
        ex.Message.Verify().ToContain("must start with a dot");
    }

    [Fact]
    public void Validate_TopLevelMacOsFileAssociationWithoutBundlePath_Throws()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Build.Targets = ["osx-arm64"];
        manifest.FileAssociations =
        [
            new FileAssociation
            {
                Extension = ".oef",
                Description = "OEF File",
                Command = "open %1",
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Verify().ToContain("file_associations[0].bundle_path");
    }

    [Fact]
    public void Validate_TopLevelFileAssociationValid_Passes()
    {
        var manifest = CreateBaseManifest("user");
        manifest.FileAssociations =
        [
            new FileAssociation
            {
                Extension = ".oef",
                Description = "OEF File",
                Command = "\"app.exe\" \"%1\"",
            },
        ];

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
            Ui = new UiConfiguration { WizardSteps = [] },
            Files = [new FilesEntry { SourceDir = ".", Include = ["*.txt"] }],
        };
    }
}
