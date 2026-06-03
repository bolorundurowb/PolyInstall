namespace PolyInstall.Install;

public static class InstallDirectoryEstimator
{
    /// <summary>Estimated folder size in KiB (for ARP EstimatedSize).</summary>
    public static long EstimateKibRecursive(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            return 0;
        long bytes = 0;
        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            try
            {
                bytes += new FileInfo(path).Length;
            }
            catch
            {
            }
        }

        return Math.Max(1, bytes / 1024);
    }
}
