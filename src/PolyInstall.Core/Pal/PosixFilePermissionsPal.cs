namespace PolyInstall.Pal;

internal sealed class PosixFilePermissionsPal : IFilePermissionsPal
{
    public void SetFileMode(string path, int mode)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            if (mode is < 0 or > 0xFFF)
                throw new InvalidOperationException($"File mode {mode} is outside the Unix permission range.");

            File.SetUnixFileMode(path, (UnixFileMode)mode);
            return;
        }

        throw new PlatformNotSupportedException("Permission tasks are not supported on this platform.");
    }
}
