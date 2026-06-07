using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

public class ServiceLifecycleTests
{
    [Fact]
    public void InstallCoordinator_Run_RegistersServicesAndPersistsState()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        manifest.Build.InstallerTarget = "linux-x64";
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "com.example.sample",
                Require = CurrentRequire(),
                Scope = "user",
                Executable = "{AppDir}/app",
                Arguments = ["--service"],
                Enabled = true,
                Start = true,
            },
        ];

        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        var servicePal = new RecordingServiceManagerPal();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app"), "payload");

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot, servicePal),
            });

            servicePal.Installed.Should().ContainSingle();
            servicePal.Installed[0].Executable.Should().Be(Path.Combine(installRoot, "app"));
            servicePal.Installed[0].Arguments.Should().Equal("--service");
            result.State.RegisteredServices.Should().ContainSingle();
            InstallStateIo.ReadState(installRoot).RegisteredServices.Should().ContainSingle();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Update_RemovesStaleRegisteredServices()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        var servicePal = new RecordingServiceManagerPal();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app"), "payload");
            var previousState = TestHelpers.StateFor(manifest, installRoot, "1.0.0", ["app"]);
            previousState.RegisteredServices =
            [
                new RegisteredServiceInfo
                {
                    Name = "com.example.old",
                    Scope = "user",
                    Platform = CurrentPlatform(),
                    UnitPath = "old.service",
                    Enabled = true,
                },
            ];
            InstallStateIo.WriteState(installRoot, previousState);
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot, servicePal),
                ExistingInstall = existing,
            });

            servicePal.Removed.Should().ContainSingle();
            servicePal.Removed[0].Name.Should().Be("com.example.old");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    private static string CurrentRequire() =>
        OperatingSystem.IsWindows()
            ? "os.isWindows"
            : OperatingSystem.IsMacOS()
                ? "os.isMacOS"
                : "os.isLinux";

    private static string CurrentPlatform() =>
        OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS()
                ? "macos"
                : "linux";

    private sealed class TestPal(string appDir, IServiceManagerPal services) : IPolyInstallPal
    {
        public string AppDir { get; } = appDir;
        public string ProgramFiles => System.IO.Path.GetTempPath();
        public string UserHome => System.IO.Path.GetTempPath();
        public string Desktop => System.IO.Path.GetTempPath();
        public IShortcutPal Shortcuts { get; } = new NoOpShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IPathPal? Path => null;
        public IFileAssociationPal? FileAssociations => null;
        public IServiceManagerPal? Services { get; } = services;
    }

    private sealed class RecordingServiceManagerPal : IServiceManagerPal
    {
        private readonly List<RegisteredServiceInfo> _registered = [];

        public List<ServiceRegistrationInfo> Installed { get; } = [];
        public List<RegisteredServiceInfo> Removed { get; } = [];
        public IReadOnlyList<RegisteredServiceInfo> RegisteredServices => _registered;

        public void InstallOrUpdate(ServiceRegistrationInfo service)
        {
            Installed.Add(service);
            _registered.Add(new RegisteredServiceInfo
            {
                Name = service.Name,
                Scope = service.Scope,
                Platform = CurrentPlatform(),
                Enabled = service.Enabled,
                Started = service.Start,
            });
        }

        public void Remove(RegisteredServiceInfo service)
        {
            Removed.Add(service);
        }
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
