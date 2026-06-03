namespace PolyInstall.Core.Install;

public static class DirectoryCopy
{
    public static void CopyRecursive(
        string sourceDir,
        string destDir,
        CancellationToken ct = default,
        Action<string>? onFileCopying = null)
        => CopyRecursive(sourceDir, destDir, Path.GetFullPath(sourceDir), ct, onFileCopying);

    private static void CopyRecursive(
        string sourceDir,
        string destDir,
        string sourceRoot,
        CancellationToken ct,
        Action<string>? onFileCopying)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            onFileCopying?.Invoke(Path.GetRelativePath(sourceRoot, file));
            File.Copy(file, Path.Combine(destDir, name), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            CopyRecursive(dir, Path.Combine(destDir, name), sourceRoot, ct, onFileCopying);
        }
    }
}
