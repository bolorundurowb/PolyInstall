namespace PolyInstall.Core.Install;

public static class PayloadFileInventory
{
    public static List<string> Enumerate(string payloadRoot)
    {
        if (!Directory.Exists(payloadRoot))
            return [];

        return Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(payloadRoot, path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void DeleteFilesMissingFromNewPayload(
        string installRoot,
        IEnumerable<string>? previousPayloadFiles,
        IReadOnlySet<string> newPayloadFiles,
        Action<string>? onFileDeleted = null)
    {
        if (previousPayloadFiles is null)
            return;

        foreach (var previousFile in previousPayloadFiles)
        {
            var relativePath = NormalizeRelativePath(previousFile);
            if (newPayloadFiles.Contains(relativePath))
                continue;
            if (IsGeneratedInstallArtifact(relativePath))
                continue;

            var fullPath = SafeCombineUnderRoot(installRoot, relativePath);
            if (fullPath is null || !File.Exists(fullPath))
                continue;

            File.Delete(fullPath);
            onFileDeleted?.Invoke(relativePath);
            TryDeleteEmptyParentDirectories(installRoot, Path.GetDirectoryName(fullPath));
        }
    }

    public static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private static bool IsGeneratedInstallArtifact(string relativePath) =>
        relativePath.Equals($"{InstallStatePaths.PolyDirName}/{InstallStatePaths.InstallStateFileName}", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals($"{InstallStatePaths.PolyDirName}/{InstallStatePaths.EmbeddedManifestFileName}", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals(InstallStatePaths.UninstallExeFileName, StringComparison.OrdinalIgnoreCase);

    private static string? SafeCombineUnderRoot(string root, string relativePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (combined.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || combined.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return combined;
        }

        return null;
    }

    private static void TryDeleteEmptyParentDirectories(string installRoot, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        while (current.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(current) && Directory.EnumerateFileSystemEntries(current).FirstOrDefault() is null)
                    Directory.Delete(current);
            }
            catch
            {
                return;
            }

            current = Path.GetDirectoryName(current) ?? "";
        }
    }
}
