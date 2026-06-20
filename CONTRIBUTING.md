# Contributing to PolyInstall

Thanks for contributing to PolyInstall.

This guide covers contributor workflow: prerequisites, build, test, and pull requests. For system design and component boundaries, see [ARCHITECTURE.md](ARCHITECTURE.md). If you only want to use PolyInstall to build installers, see the [documentation](https://bolorundurowb.github.io/PolyInstall/).

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
2. Make focused changes with tests when behaviour changes.
3. Run restore/build/test locally before opening a PR.
4. Update docs/examples/schema when relevant.

## Schema changes

If you change manifest models or validation behaviour, regenerate the schema:

```bash
dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj
```

Commit `schema/v1.json` updates alongside the related code changes.

## Validate a sample installer flow

See [ARCHITECTURE.md](ARCHITECTURE.md) for stub layout, publish commands, and a local smoke-test loop using `examples/polyinstall.sample.yaml`.

## Pull request expectations

- Keep PRs focused and reviewable.
- Include or update tests for functional changes.
- Keep documentation aligned with behaviour changes.
- Ensure CI is passing before merge.

## Commit messages

Use concise, intention-revealing commit messages that explain the reason for the change.

## Release notes

Releases are created automatically by `.github/workflows/generate-release.yml` when the `<Version>` in `src/Directory.Build.props` does not yet have a matching `v<Version>` tag on the remote. Bump `<Version>` when you want a new release; if the tag already exists, the workflow skips. Pre-releases are marked when the version string contains `-alpha` or `-beta` (case-insensitive). See [ARCHITECTURE.md](ARCHITECTURE.md) for release artifact layout. Do not edit release assets manually.
