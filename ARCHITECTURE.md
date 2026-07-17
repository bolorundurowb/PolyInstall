# PolyInstall architecture

This document describes how PolyInstall is structured: projects, data flow, bundle format, and runtime behaviour. For day-to-day contributor workflow (build, test, PRs), see [CONTRIBUTING.md](CONTRIBUTING.md). For end-user usage, see the [documentation site](https://bolorundurowb.github.io/PolyInstall/).

## Overview

PolyInstall separates **build time** from **run time**:

1. **Build time** — The `polyinstall` CLI reads a YAML manifest, validates it, globs application files, compresses them into a zip payload, and **appends** manifest + payload to a pre-published **stub** executable (`PolyInstall.Runtime`). The output is a self-extracting installer binary per `build.targets` entry.

2. **Run time** — The stub reads the appended bundle from its own executable path, decompresses the payload to a temp directory, shows an Avalonia wizard (`PolyInstall.UI`), copies files to the chosen install directory, runs tasks (shortcuts, registry, services, etc.), and writes install metadata under `.polyinstall/`.

On Windows, when Add/Remove Programs registration is enabled, the CLI also embeds a trimmed **`PolyInstall.Uninstall`** host in the payload. After install it is copied to `Uninstall.exe` at the install root for ARP and command-line uninstall.

```
┌─────────────────┐     YAML manifest + payload files
│  polyinstall    │
│  CLI            │──── glob → zip → compress → append to stub
└────────┬────────┘
         │
         ▼
┌─────────────────┐     User runs installer
│ PolyInstall.    │──── read footer → decompress → extract → wizard
│ Runtime (stub)  │──── InstallCoordinator → tasks / services / state
└─────────────────┘
```

## Solution layout

Solution file: `src/PolyInstall.slnx`.

| Project | Role |
|---------|------|
| **`PolyInstall.Core`** | Shared manifest models, bundle read/write, install/uninstall coordinators, task engine, platform abstraction (PAL), conditions, path placeholders. No YAML or Avalonia dependencies. |
| **`PolyInstall.Core.Build`** | Build-only utilities: YAML parsing (`YamlDotNet`), file globbing, semantic manifest validation. Keeps runtime stubs free of these dependencies. |
| **`PolyInstall.Cli`** | `polyinstall` executable: `build` and `validate` commands, installer pipeline, AppImage/DMG packaging, signing. References Core + Core.Build. |
| **`PolyInstall.Runtime`** | Published **installer stub**: self-contained, trimmed, single-file Avalonia host. References Core + UI. |
| **`PolyInstall.UI`** | Avalonia wizard (`MainWindow`, `App`). Reads `InstallBootstrap` populated by the runtime host. |
| **`PolyInstall.Uninstall`** | Windows-only trimmed uninstall host (no appended payload). Reads `.polyinstall/` state from disk. |
| **`PolyInstall.SchemaGen`** | Generates `schema/v1.json` from C# manifest models via NJsonSchema. |
| **`PolyInstall.Core.Tests`** | Unit tests for Core (payload trailer, install/update/uninstall, tasks, PAL behaviour). |
| **`PolyInstall.Core.Build.Tests`** | Tests for YAML parsing, globbing, semantic validation. |

### Dependency graph

```
PolyInstall.Cli ──► PolyInstall.Core.Build ──► PolyInstall.Core
PolyInstall.Runtime ──► PolyInstall.UI ──► PolyInstall.Core
PolyInstall.Uninstall ──► PolyInstall.Core
PolyInstall.SchemaGen ──► PolyInstall.Core
```

**Design intent:** Runtime and uninstall stubs depend only on `PolyInstall.Core` (and UI for the installer). Build-time parsing and validation live in `PolyInstall.Core.Build` and `PolyInstall.Cli` so published stubs stay small and dependency-light.

## Bundle binary format

The CLI does **not** rebuild the stub per package. It copies stub bytes, then appends:

```text
[original stub executable][manifest UTF-8 JSON][compressed zip payload][20-byte footer]
```

Footer layout (little-endian):

| Offset | Size | Content |
|--------|------|---------|
| 0 | 8 | Compressed payload length (`uint64`) |
| 8 | 4 | Manifest length (`uint32`) |
| 16 | 8 | Magic `POLYIN01` |

Implementation: `PolyInstall.Core/Payload/InstallPayloadTrailer.cs`, `InstallBundleReader.cs`, `PayloadArchive.cs`.

The reader checks the physical end of the file first, then scans backward for the magic bytes so **signed** installers still work when signing tools append data after the PolyInstall footer.

Payload compression is **Brotli** or **GZip** (configured by `build.compression`). The compressed blob is a zip archive of matched manifest files (paths relative to each `files[].source_dir`).

## Manifest pipeline

| Stage | Format | Location |
|-------|--------|----------|
| Authoring | YAML, snake_case keys | User manifest file |
| Parse | C# `InstallManifest` and related models | `PolyInstall.Core/Manifest/` |
| Env substitution | `${VAR}` / `${VAR:-default}` on all string values | `EnvironmentSubstitution` (CLI `build`/`validate` only) |
| JSON Schema validation | `schema/v1.json` | `ManifestJsonValidator` (Cli) |
| Semantic validation | Cross-field rules beyond schema | `ManifestSemanticValidator` (Core.Build) |
| Embedded runtime JSON | snake_case JSON in bundle | Serialized by CLI; read by stub |

Regenerate schema after model changes:

```bash
dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj
```

Commit `schema/v1.json` with the related code changes.

## CLI build pipeline

Entry: `PolyInstall.Cli/Program.cs` → `InstallerBuildPipeline.RunAsync`.

Per `build.targets` entry:

1. Parse YAML and apply environment substitution.
2. Validate against JSON Schema and semantic rules.
3. Glob files from `files[]`; build `PayloadFeatureIndex` when `features` are defined.
4. Resolve stub path: `--stubs`, or `stubs/` next to the CLI, or `<base>/stubs`; optional `build.stub_path` with `{rid}` token.
5. For Windows targets with `register_arp: true`, embed signed `PolyInstall.Uninstall.exe` at `.polyinstall/tools/PolyInstall.Uninstall.exe`.
6. Pack files to zip, compress, append to `PolyInstall.Runtime` stub.
7. Optionally sign (Windows `signtool`, macOS `codesign` / `notarytool`).
8. Optionally package AppImage (`AppImagePackager`, Linux host + `mksquashfs`) or DMG (`DmgPackager`, macOS host + `hdiutil`).

Manifest target tokens map to .NET RIDs via `RidMapping` (e.g. `windows-x64` → `win-x64`).

## Runtime installer flow

Entry: `PolyInstall.Runtime/Program.cs`.

1. Read embedded manifest from own executable (`InstallBundleReader.ReadManifestFromSeekableFile`).
2. Create `DefaultPolyInstallPal`; locate existing install via `InstalledProductLocator` (product id / version).
3. On Windows machine-scope installs or Windows services, relaunch elevated via UAC when needed.
4. Decompress payload to temp zip, extract to temp directory (`ZipPayloadExtractor`).
5. `InstallBootstrap.Init(manifest, extractRoot, pal, existingInstall)` — shared state for UI and install logic.
6. Start Avalonia (`PolyInstall.UI`).

### Wizard UI

`MainWindow` drives steps from `ui.wizard_steps` (or a default welcome → destination → progress → finish flow). Step types include `welcome`, `eula`, `destination`, `features`, `progress`, `finish`. The `progress` step invokes `InstallCoordinator.Run` on a background thread with progress callbacks.

Feature selection is stored in `InstallBootstrap.SelectedFeatures` and gates file copy, tasks, associations, and services.

### Install coordinator

`InstallCoordinator.Run` orchestrates:

- Resolve install mode (fresh install vs update/repair) from existing `.polyinstall/install-state.json`.
- **Pre-install** tasks (`TaskEngine`).
- Remove stale services from a prior install when updating.
- Copy allowed payload files (core + selected features) via `DirectoryCopy`; prune obsolete files on update.
- **Post-install** tasks.
- Register `file_associations` and `services` from the manifest.
- `InstallFinalizer` — write `.polyinstall/embedded-manifest.json` and `.polyinstall/install-state.json`; on Windows copy uninstall stub to `Uninstall.exe` and register ARP when configured.

### Task engine

`TaskEngine` runs manifest `tasks` phases (`pre_install`, `post_install`, `pre_uninstall`, `post_uninstall`). Each task has an `action`, optional `require` (OS predicates via `ConditionEvaluator`), optional `features` gating, and `parameters` with path placeholder expansion.

Supported actions include `create_shortcut`, `write_registry`, `create_desktop_entry`, `set_permissions`, `add_to_path`, `file_association`. Platform-specific work is delegated to the PAL.

## Platform abstraction layer (PAL)

`IPolyInstallPal` (`PolyInstall.Core/Pal/`) exposes OS-specific capabilities:

| Capability | Windows | Linux | macOS |
|------------|---------|-------|-------|
| Shortcuts | `.lnk` via PowerShell | symlink / wrapper | symlink / wrapper |
| Registry | `WindowsRegistryPal` | — | — |
| Desktop entries | — | `LinuxDesktopEntryPal` | — |
| File permissions | — | `PosixFilePermissionsPal` | `PosixFilePermissionsPal` |
| File associations | `WindowsFileAssociationPal` | `LinuxFileAssociationPal` | `MacOsFileAssociationPal` |
| Services | `WindowsServiceManagerPal` (`sc.exe`) | `LinuxSystemdServiceManagerPal` | `MacOsLaunchdServiceManagerPal` |
| PATH | `PathPal` | `PathPal` | `PathPal` |

`DefaultPolyInstallPal` wires implementations based on `OperatingSystem` checks. Path placeholders (`{AppDir}`, `{ProgramFiles}`, `{LocalAppData}`, `{UserHome}`, `{Desktop}`) are expanded via `InstallPathResolver` / PAL `AppDir`. `{LocalAppData}` resolves to .NET's local application-data directory (and falls back to the user home directory when unavailable).

## Install state

After a successful install or update, PolyInstall writes under `<install-dir>/.polyinstall/`:

| File | Purpose |
|------|---------|
| `install-state.json` | Product id, version, install location, scope, payload file list, selected features, registered services, PATH additions, ARP key path |
| `embedded-manifest.json` | Full manifest JSON for uninstall and future updates |
| `tools/PolyInstall.Uninstall.exe` | Bundled uninstall host (Windows, when ARP enabled) |

`InstalledProductLocator` uses this state to detect existing installs and enable update/repair flows.

## Windows uninstall host

`PolyInstall.Uninstall` is a separate trimmed executable with **no** appended bundle. Flow:

1. Parse `--uninstall` / `--quiet` / optional `--install-location`.
2. Read state and embedded manifest from `.polyinstall/`.
3. Relaunch elevated if system-scope Windows services were registered.
4. `UninstallCoordinator.Run` — uninstall tasks, unregister associations, stop/remove services, remove PATH entries, ARP unregister, delete payload files, schedule install root deletion (including `Uninstall.exe` on Windows).

The self-extracting **installer** stub does not implement `--uninstall`.

## Stub publishing

Stubs are published per .NET RID before building installers:

```bash
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r win-x64 -o stubs/win-x64
```

Windows targets with `register_arp: true` also need the uninstall stub in the same folder:

```bash
dotnet publish src/PolyInstall.Uninstall/PolyInstall.Uninstall.csproj -c Release -r win-x64 -o stubs/win-x64
```

Expected layout:

```text
stubs/
  win-x64/
    PolyInstall.Runtime.exe
    PolyInstall.Uninstall.exe    # Windows + register_arp
  linux-x64/
    PolyInstall.Runtime
  osx-arm64/
    PolyInstall.Runtime
```

`PolyInstall.Runtime` is configured for single-file, self-contained, trimmed publish (`PublishSingleFile`, `PublishTrimmed`, `SelfContained`).

### Local smoke test

```bash
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r win-x64 -o stubs/win-x64
dotnet publish src/PolyInstall.Uninstall/PolyInstall.Uninstall.csproj -c Release -r win-x64 -o stubs/win-x64
dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- build examples/polyinstall.sample.yaml --base examples --stubs stubs
```

Run the produced binary on a matching OS. Linux AppImage requires `mksquashfs` on a Linux host; macOS DMG requires `hdiutil` on macOS.

## Release artifacts

Releases are produced by `.github/workflows/generate-release.yml` when `src/Directory.Build.props` `<Version>` has no matching `v<Version>` tag on the remote.

| Artifact | Contents |
|----------|----------|
| `polyinstall-<rid>-<tag>.zip` | Self-contained CLI, `schema/v1.json`, `stubs/` for that zip's host RID |
| `stubs-<rid>-<tag>.zip` | Cross-target stub folders for building installers for other RIDs |
| `SHA256SUMS.txt` | Checksums for release zips |

Bump `<Version>` in `src/Directory.Build.props` to trigger a new release. Pre-releases are marked when the version contains `-alpha` or `-beta`.

## Testing

| Project | Focus |
|---------|-------|
| `PolyInstall.Core.Tests` | Payload trailer scanning, bundle read, install/update/uninstall coordinators, tasks, feature filtering, path resolution |
| `PolyInstall.Core.Build.Tests` | YAML parsing, glob resolver, semantic validation |

Run all tests:

```bash
dotnet test src/PolyInstall.slnx -c Release
```

CI runs tests on Ubuntu and macOS before release publish. Coverage uses Cobertura under `src/coverage/<test-project>/` when `/p:CollectCoverage=true` is set.
