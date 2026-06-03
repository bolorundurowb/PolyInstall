using System.IO.Compression;

namespace PolyInstall.Install;

public static class ZipPayloadExtractor
{
    public static void ExtractToDirectory(byte[] zipBytes, string destinationDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        using var ms = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
                continue;
            var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(targetPath, Path.GetFullPath(destinationDirectory), StringComparison.Ordinal))
                throw new InvalidOperationException($"Zip entry escapes destination: {entry.FullName}");

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }
}
