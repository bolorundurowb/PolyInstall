namespace PolyInstall.Install;

public static class DirectoryCopy
{
    public static void CopyRecursive(
        string sourceDir,
        string destDir,
        CancellationToken ct = default,
        Action<string>? onFileCopied = null)
        => CopyRecursive(sourceDir, destDir, Path.GetFullPath(sourceDir), allowedRelativePaths: null, ct, onFileCopied);

    /// <summary>
    /// Copies files from <paramref name="sourceDir"/> to <paramref name="destDir"/>, skipping files
    /// whose forward-slash relative path (relative to <paramref name="sourceDir"/>) is not present in
    /// <paramref name="allowedRelativePaths"/>. Pass a case-insensitive set built from
    /// <see cref="PayloadFileInventory.NormalizeRelativePath(string)"/> paths.
    /// </summary>
    public static void CopyRecursive(
        string sourceDir,
        string destDir,
        IReadOnlySet<string> allowedRelativePaths,
        CancellationToken ct = default,
        Action<string>? onFileCopied = null)
        => CopyRecursive(sourceDir, destDir, Path.GetFullPath(sourceDir), allowedRelativePaths, ct, onFileCopied);

    private static void CopyRecursive(
        string sourceDir,
        string destDir,
        string sourceRoot,
        IReadOnlySet<string>? allowedRelativePaths,
        CancellationToken ct,
        Action<string>? onFileCopied)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (allowedRelativePaths is not null)
            {
                var rel = PayloadFileInventory.NormalizeRelativePath(Path.GetRelativePath(sourceRoot, file));
                if (!allowedRelativePaths.Contains(rel))
                    continue;
            }
            File.Copy(file, Path.Combine(destDir, name), overwrite: true);
            onFileCopied?.Invoke(Path.GetRelativePath(sourceRoot, file));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            CopyRecursive(dir, Path.Combine(destDir, name), sourceRoot, allowedRelativePaths, ct, onFileCopied);
        }
    }
}
