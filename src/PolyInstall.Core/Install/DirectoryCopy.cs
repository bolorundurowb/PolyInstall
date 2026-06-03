namespace PolyInstall.Install;

public static class DirectoryCopy
{
    public static void CopyRecursive(
        string sourceDir,
        string destDir,
        CancellationToken ct = default,
        Action<string>? onFileCopied = null)
        => CopyRecursive(sourceDir, destDir, Path.GetFullPath(sourceDir), ct, onFileCopied);

    private static void CopyRecursive(
        string sourceDir,
        string destDir,
        string sourceRoot,
        CancellationToken ct,
        Action<string>? onFileCopied)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, name), overwrite: true);
            onFileCopied?.Invoke(Path.GetRelativePath(sourceRoot, file));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            CopyRecursive(dir, Path.Combine(destDir, name), sourceRoot, ct, onFileCopied);
        }
    }
}
