using System.IO.Compression;
using PolyInstall.Payload;

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

    public static void ExtractStreamToDirectory(
        Stream zipStream,
        string destinationDirectory,
        CancellationToken ct = default,
        int maxEntries = InstallPayloadLimits.MaxZipEntries,
        long maxTotalUncompressedBytes = InstallPayloadLimits.MaxDecompressedPayloadBytes)
    {
        Directory.CreateDirectory(destinationDirectory);
        var fullDestination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        var comparison = RelativePathGuard.PathComparison;

        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entryCount = 0;
        long totalUncompressed = 0;
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (++entryCount > maxEntries)
                throw new InvalidDataException(
                    $"Payload zip has more than {maxEntries.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} entries.");

            var targetPath = Path.GetFullPath(Path.Combine(fullDestination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(fullDestination + Path.DirectorySeparatorChar, comparison)
                && !string.Equals(targetPath, fullDestination, comparison))
                throw new InvalidOperationException($"Zip entry escapes destination: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                if (entry.FullName.EndsWith('/'))
                    Directory.CreateDirectory(targetPath);
                continue;
            }

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var source = entry.Open())
            using (var target = File.Create(targetPath))
            {
                totalUncompressed += InstallPayloadLimits.CopyWithLimit(
                    source,
                    target,
                    maxTotalUncompressedBytes - totalUncompressed,
                    ct);
            }

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
