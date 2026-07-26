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
        }
    }
}
