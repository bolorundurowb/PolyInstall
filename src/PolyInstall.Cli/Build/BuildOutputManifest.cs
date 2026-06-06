namespace PolyInstall.Cli.Build;

/// <summary>
/// Describes the artifacts produced by a <c>polyinstall build</c> run.
/// </summary>
public sealed record BuildOutputManifest(
    string ProductName,
    string Version,
    List<BuildArtifact> Artifacts);

/// <summary>
/// A single artifact produced by the build pipeline.
/// </summary>
public sealed record BuildArtifact(
    string Target,
    string Rid,
    string Type,
    string Path,
    long Size);
