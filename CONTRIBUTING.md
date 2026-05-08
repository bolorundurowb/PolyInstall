# Contributing to PolyInstall

Thanks for contributing to PolyInstall.

This guide is for engineers working on the source repository (features, bug fixes, tests, docs, and release prep). If you only want to use PolyInstall to build installers, use the usage guide in `README.md`.

## Development prerequisites

- .NET SDK 10.x
- Git
- Optional: Linux/macOS tooling if you are validating platform packaging end-to-end:
  - Linux AppImage packaging needs `mksquashfs` (`squashfs-tools`)
  - macOS DMG packaging needs `hdiutil` on macOS

## Clone and build

```bash
git clone <your-fork-or-repo-url>
cd PolyInstall
dotnet restore src/PolyInstall.slnx
dotnet build src/PolyInstall.slnx -c Release --no-restore
```

## Run tests

```bash
dotnet test src/PolyInstall.slnx -c Release --no-build --verbosity normal
```

To run with coverage (same shape as CI; Cobertura files land under `src/coverage/<test-project-name>/`):

```bash
dotnet test src/PolyInstall.slnx -c Release --no-build /p:CollectCoverage=true
```

## Local development workflow

1. Create a feature branch from `master`.
2. Make focused changes with tests when behavior changes.
3. Run restore/build/test locally before opening a PR.
4. Update docs/examples/schema when relevant.

## Schema changes

If you change manifest models or validation behavior, regenerate the schema:

```bash
dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj
```

Commit `schema/v1.json` updates alongside the related code changes.

## Validate a sample installer flow

Quick smoke test loop:

1. Publish a runtime stub for your target RID.
2. Build an installer from `examples/polyinstall.sample.yaml`.
3. Run the produced installer on a matching OS.

Example commands:

```bash
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r win-x64 -o stubs/win-x64
dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- build examples/polyinstall.sample.yaml --base examples --stubs stubs
```

## Pull request expectations

- Keep PRs focused and reviewable.
- Include or update tests for functional changes.
- Keep documentation aligned with behavior changes.
- Ensure CI is passing before merge.

## Commit messages

Use concise, intention-revealing commit messages that explain the reason for the change.

## Release notes

Releases are created by `.github/workflows/generate-release.yml` on every push to `master` when the `<Version>` in `src/Directory.Build.props` does not yet have a matching git tag `v<Version>` on the remote. The workflow runs tests, publishes self-contained CLI zips (Windows, Linux, macOS), creates that tag on the pushed commit, and opens a GitHub Release with those assets. Bump `<Version>` in `src/Directory.Build.props` when you want a new release; if the tag already exists, the workflow skips. Pre-releases are marked when the version string contains `-alpha` or `-beta` (case-insensitive). Do not edit release assets manually.

