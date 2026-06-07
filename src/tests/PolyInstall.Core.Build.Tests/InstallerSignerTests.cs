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

        command.FileName.Verify().ToBe("signtool");
        command.Arguments.SequenceEqual(
            [
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
                "dist/App-windows-x64.exe",
            ]).Verify().ToBeTrue();
        command.SecretArguments.Verify().ToContain("pfx-secret");
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

        ContainsInOrder(command.Arguments, "/sha1", "abcdef", "/s", "My", "/sm").Verify().ToBeTrue();
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

        command.FileName.Verify().ToBe("codesign");
        command.Arguments.SequenceEqual(
            [
                "--force",
                "--sign",
                "Developer ID Application: Example",
                "--timestamp",
                "--options",
                "runtime",
                "--keychain",
                "build.keychain-db",
                "dist/App-osx-arm64",
            ]).Verify().ToBeTrue();
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

        command.FileName.Verify().ToBe("xcrun");
        command.Arguments.SequenceEqual(
            [
                "notarytool",
                "submit",
                "dist/App-osx-arm64.dmg",
                "--keychain-profile",
                "polyinstall-notary",
                "--wait",
            ]).Verify().ToBeTrue();
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

            runner.Commands.Verify().ToHaveCount(1);
            var command = runner.Commands[0];
            ContainsInOrder(command.Arguments, "/p", "env-secret").Verify().ToBeTrue();
            command.SecretArguments.Verify().ToContain("env-secret");
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

        (await act.ThrowsAsync<InvalidOperationException>())
            .WithMessageContaining($"'{envName}'")
            .WithMessageContaining("not set");
        runner.Commands.Verify().ToBeEmpty();
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

        runner.Commands.Verify().ToHaveCount(3);
        runner.Commands[0].Description.Verify().ToBe("Signing macOS DMG");
        runner.Commands[0].FileName.Verify().ToBe("codesign");
        runner.Commands[1].Description.Verify().ToBe("Notarizing macOS DMG");
        ContainsInOrder(runner.Commands[1].Arguments, "notarytool", "submit", "dist/App-osx-arm64.dmg")
            .Verify().ToBeTrue();
        runner.Commands[2].Description.Verify().ToBe("Stapling macOS notarization ticket");
        runner.Commands[2].Arguments.SequenceEqual(["stapler", "staple", "dist/App-osx-arm64.dmg"])
            .Verify().ToBeTrue();
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

        runner.Commands.Verify().ToHaveCount(1);
        runner.Commands[0].Description.Verify().ToBe("Signing macOS DMG");
    }

    private static bool ContainsInOrder<T>(IEnumerable<T> actual, params T[] expected)
    {
        var expectedIndex = 0;
        foreach (var item in actual)
        {
            if (EqualityComparer<T>.Default.Equals(item, expected[expectedIndex]))
            {
                expectedIndex++;
                if (expectedIndex == expected.Length)
                    return true;
            }
        }

        return false;
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
