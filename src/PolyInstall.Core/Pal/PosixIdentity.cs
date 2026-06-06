using System.Diagnostics;

namespace PolyInstall.Pal;

internal static class PosixIdentity
{
    public static bool IsRoot => !OperatingSystem.IsWindows()
                                 && (Environment.UserName.Equals("root", StringComparison.Ordinal)
                                     || (TryGetUserId(out var uid) && uid == 0));

    public static uint UserId
    {
        get
        {
            return TryGetUserId(out var uid) ? uid : 0;
        }
    }

    private static bool TryGetUserId(out uint uid)
    {
        uid = 0;
        if (OperatingSystem.IsWindows())
            return true;
        if (uint.TryParse(Environment.GetEnvironmentVariable("UID"), out uid))
            return true;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "id",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-u");
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit();
            if (process?.ExitCode == 0 && uint.TryParse(output?.Trim(), out uid))
                return true;
        }
        catch
        {
        }

        return false;
    }
}
