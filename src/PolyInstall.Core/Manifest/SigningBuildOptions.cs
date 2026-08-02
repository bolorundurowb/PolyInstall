using System.ComponentModel;

namespace PolyInstall.Manifest;

public sealed class SigningBuildOptions
{
    [Description("Windows-specific signing options.")]
    public WindowsSigningOptions? Windows { get; set; }
    [Description("Linux-specific signing options.")]
    public LinuxSigningOptions? Linux { get; set; }
    [Description("macOS-specific signing options.")]
    public MacOsSigningOptions? Macos { get; set; }
}

public sealed class WindowsSigningOptions
{
    /// <summary>Optional path to signtool.exe. Defaults to resolving signtool from PATH.</summary>
    [Description("Optional path to signtool.exe. Defaults to resolving signtool from PATH.")]
    public string? ToolPath { get; set; }

    /// <summary>Path to a certificate file, commonly a PFX, resolved after environment substitution.</summary>
    [Description("Path to a certificate file, commonly a PFX, resolved after environment substitution.")]
    public string? CertificatePath { get; set; }

    /// <summary>Certificate SHA-1 thumbprint in the Windows certificate store.</summary>
    [Description("Certificate SHA-1 thumbprint in the Windows certificate store.")]
    public string? CertificateThumbprint { get; set; }

    /// <summary>Certificate subject name in the Windows certificate store.</summary>
    [Description("Certificate subject name in the Windows certificate store.")]
    public string? CertificateSubject { get; set; }

    /// <summary>Certificate store name used with store-based signing, for example My.</summary>
    [Description("Certificate store name used with store-based signing, for example 'My'.")]
    public string? StoreName { get; set; }

    /// <summary>Certificate store location: current_user or local_machine.</summary>
    [Description("Certificate store location: current_user or local_machine.")]
    public string? StoreLocation { get; set; }

    /// <summary>Name of an environment variable containing the certificate password.</summary>
    [Description("Name of an environment variable containing the certificate password.")]
    public string? CertificatePasswordEnv { get; set; }

    /// <summary>Plaintext passwords are not supported; use certificate_password_env.</summary>
    [Description("Plaintext passwords are not supported; use certificate_password_env.")]
    public string? CertificatePassword { get; set; }

    [Description("The timestamp server URL.")]
    public string? TimestampUrl { get; set; }
    [Description("The digest algorithm to use for the file.")]
    public string FileDigestAlgorithm { get; set; } = "sha256";
    [Description("The digest algorithm to use for the timestamp.")]
    public string TimestampDigestAlgorithm { get; set; } = "sha256";
}

public sealed class LinuxSigningOptions
{
}

public sealed class MacOsSigningOptions
{
    /// <summary>Optional path to codesign. Defaults to resolving codesign from PATH.</summary>
    [Description("Optional path to codesign. Defaults to resolving codesign from PATH.")]
    public string? CodesignPath { get; set; }

    /// <summary>Optional path to xcrun. Defaults to resolving xcrun from PATH.</summary>
    [Description("Optional path to xcrun. Defaults to resolving xcrun from PATH.")]
    public string? XcrunPath { get; set; }

    /// <summary>Developer ID or other codesign identity.</summary>
    [Description("Developer ID or other codesign identity.")]
    public string? Identity { get; set; }

    /// <summary>Optional keychain path or name to pass to codesign.</summary>
    [Description("Optional keychain path or name to pass to codesign.")]
    public string? Keychain { get; set; }

    [Description("Whether to include a secure timestamp.")]
    public bool Timestamp { get; set; } = true;
    [Description("Optional codesign flags.")]
    public string? Options { get; set; } = "runtime";

    /// <summary>Optional notarytool keychain profile. Requires a DMG package target.</summary>
    [Description("Optional notarytool keychain profile. Requires a DMG package target.")]
    public string? NotarizationProfile { get; set; }

    [Description("Whether to staple the notarization ticket to the application.")]
    public bool Staple { get; set; } = true;
}
