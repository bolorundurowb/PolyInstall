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
}
