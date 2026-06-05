using PolyInstall.Cli.Build;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Build.Tests;

public class InstallerSignerTests
{
    [Fact]
    public void BuildWindowsSignCommand_WithPfxAndPassword_UsesSigntoolArgumentsAndRedactsPassword()
    {
        var command = InstallerSigner.BuildWindowsSignCommand(
            "dist/App-windows-x64.exe",
            new WindowsSigningOptions
            {
                CertificatePath = "certs/app.pfx",
                TimestampUrl = "http://timestamp.example.test",
            },
            "pfx-secret");

        command.FileName.Should().Be("signtool");
        command.Arguments.Should().Equal(
            "sign",
            "/fd",
            "sha256",
            "/tr",
            "http://timestamp.example.test",
            "/td",
            "sha256",
            "/f",
            "certs/app.pfx",
            "/p",
            "pfx-secret",
            "dist/App-windows-x64.exe");
        command.SecretArguments.Should().Contain("pfx-secret");
    }

    [Fact]
    public void BuildWindowsSignCommand_WithStoreThumbprint_UsesStoreArguments()
    {
        var command = InstallerSigner.BuildWindowsSignCommand(
            "dist/App-windows-x64.exe",
            new WindowsSigningOptions
            {
                CertificateThumbprint = "abcdef",
                StoreName = "My",
                StoreLocation = "local_machine",
            },
            null);

        command.Arguments.Should().ContainInOrder("/sha1", "abcdef", "/s", "My", "/sm");
    }

    [Fact]
    public void BuildMacOsCodeSignCommand_UsesIdentityKeychainAndRuntimeOptions()
    {
        var command = InstallerSigner.BuildMacOsCodeSignCommand(
            "dist/App-osx-arm64",
            new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
                Keychain = "build.keychain-db",
            },
            "Signing macOS executable");

        command.FileName.Should().Be("codesign");
        command.Arguments.Should().Equal(
            "--force",
            "--sign",
            "Developer ID Application: Example",
            "--timestamp",
            "--options",
            "runtime",
            "--keychain",
            "build.keychain-db",
            "dist/App-osx-arm64");
    }

    [Fact]
    public void BuildMacOsNotarySubmitCommand_UsesXcrunProfileAndWait()
    {
        var command = InstallerSigner.BuildMacOsNotarySubmitCommand(
            "dist/App-osx-arm64.dmg",
            new MacOsSigningOptions
            {
                NotarizationProfile = "polyinstall-notary",
            });

        command.FileName.Should().Be("xcrun");
        command.Arguments.Should().Equal(
            "notarytool",
            "submit",
            "dist/App-osx-arm64.dmg",
            "--keychain-profile",
            "polyinstall-notary",
            "--wait");
    }

    [Fact]
    public async Task SignWindowsAsync_WithPasswordEnv_PassesPasswordToRunnerAsSecret()
    {
        var envName = "POLYINSTALL_TEST_CERT_PASSWORD_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(envName, "env-secret");
        try
        {
            var runner = new RecordingSigningProcessRunner();

            await InstallerSigner.SignWindowsAsync(
                "dist/App-windows-x64.exe",
                new WindowsSigningOptions
                {
                    CertificatePath = "certs/app.pfx",
                    CertificatePasswordEnv = envName,
                },
                CancellationToken.None,
                runner);

            runner.Commands.Should().ContainSingle();
            var command = runner.Commands[0];
            command.Arguments.Should().ContainInOrder("/p", "env-secret");
            command.SecretArguments.Should().Contain("env-secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public async Task SignWindowsAsync_WithMissingPasswordEnv_ThrowsBeforeRunningTool()
    {
        var envName = "POLYINSTALL_TEST_MISSING_CERT_PASSWORD_" + Guid.NewGuid().ToString("N");
        var runner = new RecordingSigningProcessRunner();

        var act = () => InstallerSigner.SignWindowsAsync(
            "dist/App-windows-x64.exe",
            new WindowsSigningOptions
            {
                CertificatePath = "certs/app.pfx",
                CertificatePasswordEnv = envName,
            },
            CancellationToken.None,
            runner);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{envName}'*not set*");
        runner.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task SignMacOsDmgAsync_WithNotarizationProfile_RunsSignNotarizeAndStaple()
    {
        var runner = new RecordingSigningProcessRunner();

        await InstallerSigner.SignMacOsDmgAsync(
            "dist/App-osx-arm64.dmg",
            new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
                NotarizationProfile = "polyinstall-notary",
            },
            CancellationToken.None,
            runner);

        runner.Commands.Should().HaveCount(3);
        runner.Commands[0].Description.Should().Be("Signing macOS DMG");
        runner.Commands[0].FileName.Should().Be("codesign");
        runner.Commands[1].Description.Should().Be("Notarizing macOS DMG");
        runner.Commands[1].Arguments.Should().ContainInOrder("notarytool", "submit", "dist/App-osx-arm64.dmg");
        runner.Commands[2].Description.Should().Be("Stapling macOS notarization ticket");
        runner.Commands[2].Arguments.Should().Equal("stapler", "staple", "dist/App-osx-arm64.dmg");
    }

    [Fact]
    public async Task SignMacOsDmgAsync_WithoutNotarizationProfile_OnlySignsDmg()
    {
        var runner = new RecordingSigningProcessRunner();

        await InstallerSigner.SignMacOsDmgAsync(
            "dist/App-osx-arm64.dmg",
            new MacOsSigningOptions
            {
                Identity = "Developer ID Application: Example",
            },
            CancellationToken.None,
            runner);

        runner.Commands.Should().ContainSingle();
        runner.Commands[0].Description.Should().Be("Signing macOS DMG");
    }

    private sealed class RecordingSigningProcessRunner : ISigningProcessRunner
    {
        public List<SigningCommand> Commands { get; } = [];

        public Task RunAsync(SigningCommand command, CancellationToken ct)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
