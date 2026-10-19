using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace PolyInstall.Pal;

[SupportedOSPlatform("windows")]
public sealed class WindowsElevationPal : IElevationPal
{
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public ElevationResult Relaunch(string exe, IReadOnlyList<string> args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = JoinArguments(args),
                UseShellExecute = true,
                Verb = "runas",
            });
            return ElevationResult.Started();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ElevationResult.Denied();
        }
        catch
        {
            return ElevationResult.Failed();
        }
    }

    public ElevationResult RelaunchAndWait(string exe, IReadOnlyList<string> args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = JoinArguments(args),
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
                return ElevationResult.Failed();

            process.WaitForExit();
            return ElevationResult.Completed(process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ElevationResult.Denied();
        }
        catch
        {
            return ElevationResult.Failed();
        }
    }

    internal static string JoinArguments(IEnumerable<string> args) =>
        string.Join(" ", args.Select(QuoteArgument));

    internal static string QuoteArgument(string arg)
    {
        if (arg.Length == 0)
            return "\"\"";
        if (!arg.Any(char.IsWhiteSpace) && !arg.Contains('"'))
            return arg;

        var quoted = new StringBuilder();
        quoted.Append('"');
        var pendingBackslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (c == '"')
            {
                quoted.Append('\\', pendingBackslashes * 2 + 1);
                quoted.Append('"');
            }
            else
            {
                quoted.Append('\\', pendingBackslashes);
                quoted.Append(c);
            }

            pendingBackslashes = 0;
        }

        quoted.Append('\\', pendingBackslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}
