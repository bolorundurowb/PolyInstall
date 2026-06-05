using System.ComponentModel;
using System.Diagnostics;
using PolyInstall.Manifest;

namespace PolyInstall.Cli.Build;

public sealed record SigningCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string Description,
    IReadOnlySet<string> SecretArguments);

public interface ISigningProcessRunner
{
    Task RunAsync(SigningCommand command, CancellationToken ct);
}

public sealed class SigningToolRunner : ISigningProcessRunner
{
    public async Task RunAsync(SigningCommand command, CancellationToken ct)
    {
        BuildLog.VerboseLine($"Signing: {FormatCommand(command)}");
        var psi = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in command.Arguments)
            psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi)
                                ?? throw new InvalidOperationException($"Could not start {command.FileName}.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{command.Description} failed ({process.ExitCode}): {stderr}{Environment.NewLine}{stdout}");
            }

            if (!string.IsNullOrWhiteSpace(stdout))
                BuildLog.VerboseLine(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr))
                BuildLog.VerboseLine(stderr.Trim());
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"{command.Description} could not start '{command.FileName}'. Ensure the signing tool is installed and available on PATH, or configure an explicit tool path.",
                ex);
        }
    }

    private static string FormatCommand(SigningCommand command)
    {
        static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;
        var args = command.Arguments.Select(a => command.SecretArguments.Contains(a) ? "<redacted>" : Quote(a));
        return $"{Quote(command.FileName)} {string.Join(" ", args)}";
    }
}

public static class InstallerSigner
{
    public static Task SignWindowsAsync(
        string filePath,
        WindowsSigningOptions options,
        CancellationToken ct,
        ISigningProcessRunner? runner = null)
    {
        var password = ReadOptionalPassword(options.CertificatePasswordEnv, "build.signing.windows.certificate_password_env");
        var command = BuildWindowsSignCommand(filePath, options, password);
        return (runner ?? new SigningToolRunner()).RunAsync(command, ct);
    }

    public static Task SignMacOsExecutableAsync(
        string filePath,
        MacOsSigningOptions options,
        CancellationToken ct,
        ISigningProcessRunner? runner = null)
    {
        var command = BuildMacOsCodeSignCommand(filePath, options, "Signing macOS executable");
        return (runner ?? new SigningToolRunner()).RunAsync(command, ct);
    }

    public static async Task SignMacOsDmgAsync(
        string dmgPath,
        MacOsSigningOptions options,
        CancellationToken ct,
        ISigningProcessRunner? runner = null)
    {
        runner ??= new SigningToolRunner();
        await runner.RunAsync(BuildMacOsCodeSignCommand(dmgPath, options, "Signing macOS DMG"), ct);

        if (string.IsNullOrWhiteSpace(options.NotarizationProfile))
            return;

        await runner.RunAsync(BuildMacOsNotarySubmitCommand(dmgPath, options), ct);
        if (options.Staple)
            await runner.RunAsync(BuildMacOsStapleCommand(dmgPath, options), ct);
    }

    public static SigningCommand BuildWindowsSignCommand(
        string filePath,
        WindowsSigningOptions options,
        string? certificatePassword)
    {
        var args = new List<string>
        {
            "sign",
            "/fd",
            NormalizeDigest(options.FileDigestAlgorithm),
        };
        var secrets = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(options.TimestampUrl))
        {
            args.Add("/tr");
            args.Add(options.TimestampUrl);
            args.Add("/td");
            args.Add(NormalizeDigest(options.TimestampDigestAlgorithm));
        }

        if (!string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            args.Add("/f");
            args.Add(options.CertificatePath);
            if (!string.IsNullOrEmpty(certificatePassword))
            {
                args.Add("/p");
                args.Add(certificatePassword);
                secrets.Add(certificatePassword);
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.CertificateThumbprint))
        {
            args.Add("/sha1");
            args.Add(options.CertificateThumbprint);
            AddWindowsStoreArguments(args, options);
        }
        else if (!string.IsNullOrWhiteSpace(options.CertificateSubject))
        {
            args.Add("/n");
            args.Add(options.CertificateSubject);
            AddWindowsStoreArguments(args, options);
        }

        args.Add(filePath);
        return new SigningCommand(
            string.IsNullOrWhiteSpace(options.ToolPath) ? "signtool" : options.ToolPath,
            args,
            "Signing Windows artifact",
            secrets);
    }

    public static SigningCommand BuildMacOsCodeSignCommand(
        string filePath,
        MacOsSigningOptions options,
        string description)
    {
        var args = new List<string>
        {
            "--force",
            "--sign",
            options.Identity ?? string.Empty,
        };

        if (options.Timestamp)
            args.Add("--timestamp");
        if (!string.IsNullOrWhiteSpace(options.Options))
        {
            args.Add("--options");
            args.Add(options.Options);
        }
        if (!string.IsNullOrWhiteSpace(options.Keychain))
        {
            args.Add("--keychain");
            args.Add(options.Keychain);
        }

        args.Add(filePath);
        return new SigningCommand(
            string.IsNullOrWhiteSpace(options.CodesignPath) ? "codesign" : options.CodesignPath,
            args,
            description,
            new HashSet<string>());
    }

    public static SigningCommand BuildMacOsNotarySubmitCommand(string dmgPath, MacOsSigningOptions options)
    {
        var args = new List<string>
        {
            "notarytool",
            "submit",
            dmgPath,
            "--keychain-profile",
            options.NotarizationProfile ?? string.Empty,
            "--wait",
        };
        return new SigningCommand(
            string.IsNullOrWhiteSpace(options.XcrunPath) ? "xcrun" : options.XcrunPath,
            args,
            "Notarizing macOS DMG",
            new HashSet<string>());
    }

    public static SigningCommand BuildMacOsStapleCommand(string dmgPath, MacOsSigningOptions options)
    {
        var args = new List<string>
        {
            "stapler",
            "staple",
            dmgPath,
        };
        return new SigningCommand(
            string.IsNullOrWhiteSpace(options.XcrunPath) ? "xcrun" : options.XcrunPath,
            args,
            "Stapling macOS notarization ticket",
            new HashSet<string>());
    }

    private static void AddWindowsStoreArguments(List<string> args, WindowsSigningOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.StoreName))
        {
            args.Add("/s");
            args.Add(options.StoreName);
        }

        if (string.Equals(options.StoreLocation, "local_machine", StringComparison.OrdinalIgnoreCase))
            args.Add("/sm");
    }

    private static string NormalizeDigest(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "sha256" : value.ToLowerInvariant();
    }

    private static string? ReadOptionalPassword(string? envName, string configPath)
    {
        if (string.IsNullOrWhiteSpace(envName))
            return null;

        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrEmpty(value))
            return value;

        throw new InvalidOperationException($"{configPath} points to environment variable '{envName}', but it is not set.");
    }
}
