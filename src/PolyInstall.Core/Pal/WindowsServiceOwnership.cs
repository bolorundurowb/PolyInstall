using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PolyInstall.Pal;

/// <summary>
/// Verifies whether a Windows service's configured binary lives under a given install root.
/// Elevated uninstall/update paths must only stop/delete services that provably belong to
/// the product being removed — install state is user-writable and must not be able to aim
/// elevated <c>sc.exe delete</c> calls at arbitrary services.
/// </summary>
public static class WindowsServiceOwnership
{
    private static readonly Regex PathToken = new(
        "\"(?<q>[^\"\r\n]+)\"|(?<u>\\S+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true only when <c>sc.exe qc</c> succeeds for <paramref name="serviceName"/>
    /// and the service's binary path resolves under <paramref name="installRoot"/>.
    /// Any failure (missing service, query error, unparseable output) returns false.
    /// </summary>
    public static bool IsOwnedByInstallRoot(string serviceName, string installRoot)
    {
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(serviceName)
            || string.IsNullOrWhiteSpace(installRoot))
        {
            return false;
        }

        var output = TryQueryServiceConfig(serviceName);
        if (output is null)
            return false;

        var binaryPath = TryExtractBinaryPath(output);
        if (binaryPath is null)
            return false;

        try
        {
            binaryPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(binaryPath));
        }
        catch
        {
            return false;
        }

        return ProcessPathMatcher.IsUnderDirectory(binaryPath, installRoot);
    }

    /// <summary>
    /// Extracts the first absolute-path-looking token from <c>sc.exe qc</c> output. Token-based
    /// (rather than field-label-based) so it does not depend on output localization.
    /// </summary>
    internal static string? TryExtractBinaryPath(string serviceConfigOutput)
    {
        foreach (Match match in PathToken.Matches(serviceConfigOutput))
        {
            var token = match.Groups["q"].Success ? match.Groups["q"].Value : match.Groups["u"].Value;
            if (token.Contains(":\\", StringComparison.OrdinalIgnoreCase)
                || (token.StartsWith('%') && token.IndexOf('%', 1) > 0))
            {
                return token;
            }
        }

        return null;
    }

    private static string? TryQueryServiceConfig(string serviceName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("qc");
            psi.ArgumentList.Add(serviceName);

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }
}
