using System.Diagnostics;

namespace PolyInstall.Pal;

internal sealed class UnixFilePermissionsPal : IFilePermissionsPal
{
    public void SetUnixFileMode(string path, int mode)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "chmod",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Convert.ToString(mode, 8));
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"chmod failed for {path}.");
    }
}
