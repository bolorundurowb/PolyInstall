namespace PolyInstall.Pal;

internal sealed class PathPal : IPathPal
{
    private readonly List<(string Path, string Scope)> _addedPaths = [];

    public void AddToPath(string path, string scope)
    {
        if (OperatingSystem.IsWindows())
            WindowsPathPal.AddToPath(path, scope);
        else
            PosixPathPal.AddToPath(path, scope);

        _addedPaths.Add((path, scope));
    }

    public void RemoveFromPath(string path, string scope)
    {
        if (OperatingSystem.IsWindows())
            WindowsPathPal.RemoveFromPath(path, scope);
        else
            PosixPathPal.RemoveFromPath(path, scope);
    }

    public IReadOnlyList<(string Path, string Scope)> AddedPaths => _addedPaths;
}
