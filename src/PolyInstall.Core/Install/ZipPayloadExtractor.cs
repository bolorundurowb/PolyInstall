using System.IO.Compression;

namespace PolyInstall.Install;

public static class ZipPayloadExtractor
{
    public static void ExtractToDirectory(byte[] zipBytes, string destinationDirectory, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(zipBytes);
        ExtractStreamToDirectory(ms, destinationDirectory, ct);
    }

    public static void ExtractFileToDirectory(string zipFilePath, string destinationDirectory, CancellationToken ct = default)
    {
        using var fs = File.OpenRead(zipFilePath);
        ExtractStreamToDirectory(fs, destinationDirectory, ct);
    }

    public static void ExtractStreamToDirectory(Stream zipStream, string destinationDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                if (entry.FullName.EndsWith('/'))
                {
                    var dirPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dirPath.StartsWith(Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !string.Equals(dirPath, Path.GetFullPath(destinationDirectory), StringComparison.Ordinal))
                        throw new InvalidOperationException($"Zip entry escapes destination: {entry.FullName}");
                    Directory.CreateDirectory(dirPath);
                }
                continue;
            }
            var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(targetPath, Path.GetFullPath(destinationDirectory), StringComparison.Ordinal))
                throw new InvalidOperationException($"Zip entry escapes destination: {entry.FullName}");

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            entry.ExtractToFile(targetPath, overwrite: true);
            RestoreUnixMode(entry, targetPath);
        }
    }

    /// <summary>
    /// Applies Unix permission bits stored in the high 16 bits of the zip external attributes.
    /// Zero means the packager recorded no mode (for example a Windows build host).
    /// </summary>
    private static void RestoreUnixMode(ZipArchiveEntry entry, string targetPath)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var bits = (entry.ExternalAttributes >> 16) & 0xFFF;
        if (bits == 0)
            return;

        File.SetUnixFileMode(targetPath, (UnixFileMode)bits);
    }
}
