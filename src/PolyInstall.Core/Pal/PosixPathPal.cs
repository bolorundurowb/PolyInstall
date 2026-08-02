namespace PolyInstall.Pal;

internal static class PosixPathPal
{
    public static void AddToPath(string directory, string scope)
    {
        ValidateDirectory(directory);
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            if (OperatingSystem.IsMacOS())
            {
                var profilePath = "/etc/paths.d";
                if (!Directory.Exists(profilePath))
                    Directory.CreateDirectory(profilePath);
                var fileName = SanitizeFileName(directory);
                File.WriteAllText(Path.Combine(profilePath, fileName), directory + "\n");
            }
            else
            {
                var profilePath = "/etc/profile.d";
                if (!Directory.Exists(profilePath))
                    Directory.CreateDirectory(profilePath);
                var fileName = SanitizeFileName(directory) + ".sh";
                File.WriteAllText(Path.Combine(profilePath, fileName),
                    BuildPathExportEntry(directory) + "\n");
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var profileFile = FindShellProfile(home);
            var entry = BuildPathExportEntry(directory);
            if (File.Exists(profileFile) && File.ReadAllText(profileFile).Contains(entry, StringComparison.Ordinal))
                return;
            File.AppendAllText(profileFile, $"\n{entry}\n");
        }
    }

    public static void RemoveFromPath(string directory, string scope)
    {
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            if (OperatingSystem.IsMacOS())
            {
                var pathsDir = "/etc/paths.d";
                var fileName = SanitizeFileName(directory);
                var filePath = Path.Combine(pathsDir, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            else
            {
                var profileDir = "/etc/profile.d";
                var fileName = SanitizeFileName(directory) + ".sh";
                var filePath = Path.Combine(profileDir, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var profileFile = FindShellProfile(home);
            if (!File.Exists(profileFile))
                return;
            string entry;
            try
            {
                entry = BuildPathExportEntry(directory);
            }
            catch (ArgumentException)
            {
                // Nothing safe to remove: an invalid directory can never have produced a
                // well-formed entry written by this installer.
                return;
            }
            var lines = File.ReadAllLines(profileFile)
                .Where(l => !l.Trim().Equals(entry, StringComparison.Ordinal))
                .ToArray();
            File.WriteAllLines(profileFile, lines);
        }
    }

    /// <summary>
    /// Builds the shell line appended to POSIX shell profiles. The directory is emitted as a
    /// single-quoted literal concatenated with the double-quoted <c>$PATH</c> reference, so
    /// shell metacharacters in install directories (<c>"</c>, <c>$</c>, backticks, <c>;</c>,
    /// <c>|</c>, <c>&amp;</c>, <c>!</c>, …) cannot break out and inject commands.
    /// </summary>
    internal static string BuildPathExportEntry(string directory)
    {
        ValidateDirectory(directory);
        return "export PATH=\"$PATH\":" + ShellSingleQuote(directory);
    }

    internal static string ShellSingleQuote(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static void ValidateDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("PATH directory must be non-empty.", nameof(directory));
        if (directory.Any(c => c is '\n' or '\r' or '\0'))
            throw new ArgumentException(
                "PATH directory contains unsupported control characters and cannot be safely added to PATH.",
                nameof(directory));
    }

    public static string FindShellProfile(string home)
    {
        var bashrc = Path.Combine(home, ".bashrc");
        if (File.Exists(bashrc))
            return bashrc;
        var zshrc = Path.Combine(home, ".zshrc");
        if (File.Exists(zshrc))
            return zshrc;
        var profile = Path.Combine(home, ".profile");
        if (File.Exists(profile))
            return profile;
        return bashrc;
    }

    public static string SanitizeFileName(string path)
    {
        var name = path.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim('_');
    }
}
