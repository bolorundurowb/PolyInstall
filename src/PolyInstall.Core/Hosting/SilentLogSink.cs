namespace PolyInstall.Hosting;

/// <summary>
/// Hardened log sink for silent installs. An elevated child must never be able to follow a
/// symlink to an arbitrary path: when <c>--log</c> is omitted the parent creates a private
/// directory under the temp root; when supplied, the path must not be a symlink. The file is
/// created with <see cref="FileMode.CreateNew"/> on first open, or appended when the elevated
/// child reopens the path the parent already created.
/// </summary>
public sealed class SilentLogSink : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    private SilentLogSink(string path, StreamWriter writer)
    {
        Path = path;
        _writer = writer;
    }

    public string Path { get; }

    /// <summary>
    /// Open a log sink. When <paramref name="requestedLogPath"/> is null, create a private
    /// directory under the system temp root and place <c>install.log</c> inside it.
    /// When <paramref name="append"/> is true, reopen an existing file created by the parent
    /// process before an elevation relaunch.
    /// </summary>
    public static SilentLogSink Open(string? requestedLogPath, bool append = false)
    {
        if (requestedLogPath is null)
            return OpenDefault();

        return OpenExplicit(requestedLogPath, append);
    }

    private static SilentLogSink OpenDefault()
    {
        var dir = CreatePrivateTempDirectory();
        var path = System.IO.Path.Combine(dir, "install.log");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        return new SilentLogSink(path, writer);
    }

    private static SilentLogSink OpenExplicit(string requestedPath, bool append)
    {
        var full = System.IO.Path.GetFullPath(requestedPath);
        var parent = System.IO.Path.GetDirectoryName(full)
                     ?? throw new InvalidOperationException("--log path has no parent directory.");

        if (!Directory.Exists(parent))
            throw new InvalidOperationException($"--log parent directory does not exist: {parent}");

        if (IsSymlinkOrReparsePoint(parent))
            throw new InvalidOperationException("--log must not resolve through a symlink.");

        var resolvedParent = System.IO.Path.GetFullPath(parent)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var fullDir = System.IO.Path.GetDirectoryName(full)!;
        if (!string.Equals(
                System.IO.Path.GetFullPath(fullDir)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
                resolvedParent,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("--log resolves outside its declared parent directory.");
        }

        FileStream stream;
        if (append)
        {
            if (!File.Exists(full))
                throw new InvalidOperationException($"--log file for append was not found: {full}");
            if (IsSymlinkOrReparsePoint(full))
                throw new InvalidOperationException("--log must not resolve through a symlink.");
            stream = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read);
        }
        else if (File.Exists(full) || Directory.Exists(full))
        {
            throw new InvalidOperationException($"--log file already exists: {full}");
        }
        else
        {
            stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }

        var writer = new StreamWriter(stream) { AutoFlush = true };
        return new SilentLogSink(full, writer);
    }

    public void WriteLine(string message)
    {
        lock (_gate)
        {
            _writer.WriteLine(message);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }

    private static string CreatePrivateTempDirectory()
    {
        var root = System.IO.Path.GetTempPath();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var name = "polyinstall-log-" + Guid.NewGuid().ToString("n");
            var dir = System.IO.Path.Combine(root, name);
            if (Directory.Exists(dir))
                continue;

            Directory.CreateDirectory(dir);
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(
                        dir,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch
                {
                    // Best-effort; the unpredictable name under the user temp root remains the primary defence.
                }
            }

            return dir;
        }

        throw new InvalidOperationException("Could not create a private log directory under the temp root.");
    }

    private static bool IsSymlinkOrReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return attrs.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }
}
