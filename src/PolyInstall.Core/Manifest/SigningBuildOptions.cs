namespace PolyInstall.Manifest;

public sealed class SigningBuildOptions
{
    public WindowsSigningOptions? Windows { get; set; }
    public LinuxSigningOptions? Linux { get; set; }
    public MacOsSigningOptions? Macos { get; set; }
}

public sealed class WindowsSigningOptions
{
    /// <summary>Optional path to signtool.exe. Defaults to resolving signtool from PATH.</summary>
    public string? ToolPath { get; set; }

    /// <summary>Path to a certificate file, commonly a PFX, resolved after environment substitution.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Certificate SHA-1 thumbprint in the Windows certificate store.</summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>Certificate subject name in the Windows certificate store.</summary>
    public string? CertificateSubject { get; set; }

    /// <summary>Certificate store name used with store-based signing, for example My.</summary>
    public string? StoreName { get; set; }

    /// <summary>Certificate store location: current_user or local_machine.</summary>
    public string? StoreLocation { get; set; }

    /// <summary>Name of an environment variable containing the certificate password.</summary>
    public string? CertificatePasswordEnv { get; set; }

    /// <summary>Plaintext passwords are not supported; use certificate_password_env.</summary>
    public string? CertificatePassword { get; set; }

    public string? TimestampUrl { get; set; }
    public string FileDigestAlgorithm { get; set; } = "sha256";
    public string TimestampDigestAlgorithm { get; set; } = "sha256";
}

public sealed class LinuxSigningOptions
{
}

public sealed class MacOsSigningOptions
{
    /// <summary>Optional path to codesign. Defaults to resolving codesign from PATH.</summary>
    public string? CodesignPath { get; set; }

    /// <summary>Optional path to xcrun. Defaults to resolving xcrun from PATH.</summary>
    public string? XcrunPath { get; set; }

    /// <summary>Developer ID or other codesign identity.</summary>
    public string? Identity { get; set; }

    /// <summary>Optional keychain path or name to pass to codesign.</summary>
    public string? Keychain { get; set; }

    public bool Timestamp { get; set; } = true;
    public string? Options { get; set; } = "runtime";

    /// <summary>Optional notarytool keychain profile. Requires a DMG package target.</summary>
    public string? NotarizationProfile { get; set; }

    public bool Staple { get; set; } = true;
}
