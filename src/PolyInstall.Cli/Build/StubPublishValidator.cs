namespace PolyInstall.Cli.Build;

/// <summary>
/// Ensures runtime stubs are published as a single-file executable suitable for the append bundle model.
/// </summary>
internal static class StubPublishValidator
{
    /// <summary>Multi-file self-contained stubs are typically well under this size; single-file Avalonia stubs are tens of MB.</summary>
    private const long MinSingleFileStubBytes = 5 * 1024 * 1024;

    public static void ValidateRuntimeStub(string stubExePath)
    {
        var stubDir = Path.GetDirectoryName(stubExePath)
                      ?? throw new InvalidOperationException($"Invalid stub path: {stubExePath}");
        var stubName = Path.GetFileNameWithoutExtension(stubExePath);
        var adjacentDll = Path.Combine(stubDir, $"{stubName}.dll");
        var hasAdjacentDll = File.Exists(adjacentDll);
        var stubSize = new FileInfo(stubExePath).Length;

        if (!hasAdjacentDll && stubSize >= MinSingleFileStubBytes)
            return;

        if (hasAdjacentDll)
        {
            throw new InvalidOperationException(
                $"""
                Runtime stub at '{stubExePath}' is a multi-file publish (found '{adjacentDll}').
                The installer build copies only the stub executable bytes; sidecar DLLs are not included in the output.
                Re-publish the stub as a single-file executable, for example:
                  dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r <rid> \
                    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
                    -o stubs/<rid>
                """);
        }

        throw new InvalidOperationException(
            $"""
            Runtime stub at '{stubExePath}' is too small ({stubSize:N0} bytes) to be a self-contained single-file host.
            Re-publish with --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true.
            """);
    }
}
