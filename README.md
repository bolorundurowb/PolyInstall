<div align="center">
  <img
    src="https://raw.githubusercontent.com/bolorundurowb/PolyInstall/refs/heads/master/assets/polyinstall-logo.svg"
    alt="poly install logo"  />
  <h1 align="center">PolyInstall</h1>
</div>

<p align="center">
  <a href="https://github.com/bolorundurowb/PolyInstall/actions/workflows/build-and-test.yml">
    <img src="https://github.com/bolorundurowb/PolyInstall/actions/workflows/build-and-test.yml/badge.svg" alt="Build, Test & Coverage" />
  </a>
  <a href="./LICENSE">
    <img alt="GitHub License" src="https://img.shields.io/github/license/bolorundurowb/polyinstall">
  </a>
</p>

**PolyInstall** is a powerful, manifest-driven installer generator. It allows you to package your applications into
cross-platform, self-extracting binaries using a single YAML configuration file. With a modern, customisable
installation UI built on **Avalonia**, PolyInstall simplifies the deployment process for Windows, Linux, and macOS.

> This project was developed with the help of **generative AI** tools.
> Treat the code, manifests, and documentation accordingly: verify behaviour, review changes before you rely on them in
> production, and apply your own judgement and testing.

## Key Features

- YAML-Based Manifests: Define your installer metadata, files, and build configurations in a single, simple YAML file.
- Cross-Platform Support: Generate self-extracting installers for Windows (.exe), Linux (AppImage), and macOS (DMG).
- Service/Daemon Registration: Register Windows services, Linux systemd units, and macOS launchd jobs from the manifest.
- Modern Avalonia UI: A clean, responsive installation interface that works across Windows, Linux, and macOS.

---

PolyInstall is a **modern toolchain** for building **self-contained installer executables** from a **YAML manifest**. At
build time, the CLI packs your application files, compresses them, and appends them (with an embedded JSON manifest) to
a **pre-published stub** — a small Avalonia-based host that extracts the payload and walks the end user through an
installer wizard.

This document is written for **consumers**: teams who want to ship installers without adopting a separate installer
product, and who are comfortable with YAML and a small command-line tool. **Prefer the self-contained `polyinstall`
binaries from [GitHub Releases](https://github.com/bolorundurowb/PolyInstall/releases)** (each CLI zip includes the
matching host stub, with separate stub archives for cross-target builds); build from source only when you contribute to
PolyInstall, need unreleased changes, or want custom stub layouts.

## What you get

| Piece                            | Role                                                                                                                                                                                                      |
|----------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **`polyinstall` CLI**            | Parses YAML, substitutes environment variables, validates against JSON Schema, globs files, builds a zip payload, compresses it, and produces one output binary per `build.targets` entry.                |
| **Stub (`PolyInstall.Runtime`)** | The actual installer binary you ship. It reads the bundle appended to itself, shows a wizard (`PolyInstall.UI`), copies files, registers services/daemons, and can run **tasks** (shortcuts, registry, `.desktop` files, permissions). |
| **`PolyInstall.Uninstall` (Windows)** | A small, trimmed **uninstall host** published beside the stub. When Windows ARP registration is enabled, the CLI embeds it in the payload as `.polyinstall/tools/PolyInstall.Uninstall.exe`; after install it is copied to **`Uninstall.exe`** at the install root for Add/Remove Programs and command-line uninstall. |
| **`schema/v1.json`**             | JSON Schema generated from the same C# models as the runtime. Use it in your editor for completion and diagnostics (see [Manifest and schema](#manifest-and-schema)).                                     |

**Platform outputs:** On **Windows**, the installer can register **Add/Remove Programs**, deploy a dedicated
**`Uninstall.exe`** (the published `PolyInstall.Uninstall` host) that runs **`--uninstall`**, and register Windows
services. Re-running a newer packaged installer for the same product detects the existing install, offers an
update/repair flow, updates files in place, removes stale services/daemons, and refreshes stored install metadata. On
**Linux**, the CLI can optionally emit an **AppImage** (requires `mksquashfs` on a Linux host) and register systemd
system/user units. On **macOS**, the CLI can optionally emit a **DMG** via `hdiutil` (requires building on macOS) and
register launchd agents/daemons.
See [Windows uninstall and ARP](#windows-uninstall-and-arp), [Linux AppImage](#linux-appimage),
and [macOS DMG](#macos-dmg).

## Requirements

**Using a [GitHub Release](https://github.com/bolorundurowb/PolyInstall/releases) build (recommended)**

- A **64-bit** host OS that matches the zip you download (Windows, Linux, or macOS).
- **No .NET SDK** is required to run `polyinstall`; the published CLI is self-contained.
- The **machine that runs your finished installer** must match the RID you built for (see [Build targets](#build-targets)).
- **Windows** installers that use `create_shortcut` tasks expect **PowerShell** on the end user’s machine (COM via
  `WScript.Shell`).
- **Windows services** require Administrator rights. PolyInstall relaunches with UAC when the manifest includes a
  Windows service.
- **Linux services** require `systemctl`; `scope: system` requires root, while `scope: user` runs through
  `systemctl --user`.
- **macOS services** require `launchctl`; `scope: system` writes a `LaunchDaemon` and requires root, while
  `scope: user` writes a `LaunchAgent`.

**Building PolyInstall from source** (optional; contributors and advanced setups)

- **.NET SDK 10** (or the version aligned with `src/Directory.Build.props` / `TargetFramework` in this repo).

## Quick start

### Recommended: GitHub Releases (compiled binaries)

Use the **pre-built** `polyinstall` from [GitHub Releases](https://github.com/bolorundurowb/PolyInstall/releases) instead
of compiling this repository yourself. Pick the zip for your **host** OS (for example `polyinstall-win-x64-<tag>.zip` on
Windows). Each CLI archive contains a self-contained `polyinstall` executable, `schema/v1.json`, and a `stubs/` tree for
that archive's RID. The release also publishes separate `stubs-<rid>-<tag>.zip` archives when you need to build for a
different target RID.

1. **Download and extract** the release zip so `polyinstall` (or `polyinstall.exe` on Windows) sits in the same folder
   as the `stubs/` directory.

2. **Author a manifest** — copy `examples/polyinstall.sample.yaml` from this repository (or start from scratch) and set
   `metadata`, `files`, and `build.targets`.

3. **Build your installer** from a shell in that extracted folder (omit `--stubs`; the CLI picks up the bundled
   `stubs/` next to the executable):

   ```bash
   ./polyinstall build /path/to/your.manifest.yaml --base /path/to/payload-root
   ```

   On Windows, run `polyinstall.exe` instead of `./polyinstall`.

4. **Run the produced `.exe`** (or non-Windows binary) on a machine that matches the target you built. It extracts to a
   temp folder and launches the wizard.

Built files land under `build.output_dir` from the manifest (relative to `--base`).

### CI: setup-polyinstall GitHub Action

In GitHub Actions, prefer the bundled setup action instead of scripting release download, extraction, executable lookup,
and checksum verification yourself:

```yaml
- uses: bolorundurowb/PolyInstall/.github/actions/setup-polyinstall@v1
  with:
    version: v0.1.0
    rid: linux-x64

- run: polyinstall build ./polyinstall.yaml --base .
```

For workflows inside this repository, use the local action path after checkout:

```yaml
- uses: actions/checkout@v6
- uses: ./.github/actions/setup-polyinstall
  with:
    version: v0.1.0
```

The action downloads the selected release zip and `SHA256SUMS.txt`, verifies the zip's SHA-256 hash, extracts the CLI,
adds it to `PATH`, and exposes `polyinstall-path`, `install-dir`, `version`, and `rid` outputs. If you download release
assets manually, download `SHA256SUMS.txt` from the same release and verify the zip before extraction, for example:

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing
```

### Optional: build PolyInstall from source

Use this path when you contribute to PolyInstall, need an unreleased build, or want to publish your own stubs (for
example extra RIDs not bundled in releases).

1. **Clone** this repository.

2. **Create a manifest** — start from `examples/polyinstall.sample.yaml` and adjust `metadata`, `files`, and
   `build.targets`.

3. **Publish the runtime stub** for each RID you need (example: Windows x64):

   ```bash
   dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r win-x64 -o stubs/win-x64
   ```

   For **Windows** targets, also publish the dedicated trimmed uninstaller into the same RID folder:

   ```bash
   dotnet publish src/PolyInstall.Uninstall/PolyInstall.Uninstall.csproj -c Release -r win-x64 -o stubs/win-x64
   ```

   The CLI looks for `PolyInstall.Runtime.exe` (Windows) or `PolyInstall.Runtime` (non-Windows) under `<stubs>/<rid>/`.
   For Windows targets with `register_arp: true` (the default), it also expects `<stubs>/<rid>/PolyInstall.Uninstall.exe`. When you omit `--stubs`, the CLI uses
   a `stubs` directory next to the `polyinstall` executable if it exists; otherwise it uses `<base>/stubs`.

4. **Build the installer**:

   ```bash
   dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- build examples/polyinstall.sample.yaml --base examples --stubs stubs
   ```

5. **Run the produced `.exe`** (or non-Windows binary) on a matching OS. It will extract to a temp folder and launch the
   wizard.

## Manifest and Schema

- **Authoring format:** YAML with **snake_case** keys (see examples).
- **Runtime format:** The stub reads an **embedded JSON** manifest (snake_case property names) produced by the CLI.
- **Schema for editors:** After building the repo, `schema/v1.json` is the source of truth. To regenerate it from the C#
  models:

  ```bash
  dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj
  ```

  Optional MSBuild integration: pass **`/p:GeneratePolyInstallSchema=true`** so `src/Directory.Build.targets` runs the
  generator before compile (opt-in).

**IDE integration:** Point your YAML at the schema for validation and IntelliSense, for example:

```yaml
# yaml-language-server: $schema=https://bolorundurowb.github.io/PolyInstall/schema/v1.json
```

If you work offline, use a **relative** or `file:` URL to `schema/v1.json` in your clone.



## Manifest structure (seven domains)

The manifest is grouped into six sections. All are represented in JSON Schema; only the fields you need must be set (defaults apply where defined in code).

### `metadata`

| Field | Meaning |
|--------|---------|
| `name` | Product name; used in default output filenames and UI. |
| `version` | Version string (semantic versioning recommended). |
| `id` | Optional stable product id. |
| `publisher` | Optional publisher label. |

### `build`

| Field | Meaning |
|--------|---------|
| `output_dir` | Directory for built installers, relative to `--base` (default in model: `dist`). |
| `compression` | `brotli` or `gzip` (see [Compression](#compression)). |
| `targets` | List of **manifest tokens** (not raw .NET RIDs); see [Build targets](#build-targets). |
| `stub_path` | Optional path to the installer stub for a target; use `{rid}` for the **.NET RID** (e.g. `C:\stubs\{rid}\PolyInstall.Runtime.exe`). If omitted, the CLI uses `<resolved-stubs-root>/<rid>/PolyInstall.Runtime[.exe]` where the resolved stubs root is `--stubs`, or `stubs` next to `polyinstall` when present, else `<base>/stubs` (and for Windows targets with `register_arp: true`, also expects `PolyInstall.Uninstall.exe` in that `<rid>` folder). |
| `windows` | Optional [Windows build options](#windows-build-options). |
| `linux` | Optional [Linux build options](#linux-build-options). |
| `macos` | Optional [macOS build options](#macos-build-options). |
| `signing` | Optional [installer signing options](#installer-signing). Omit this to build unsigned installers. |

#### Windows build options

| Field | Meaning |
|--------|---------|
| `install_scope` | `user` (default) or `machine`. Controls whether Add/Remove Programs entries go under **HKCU** or **HKLM**. |
| `register_arp` | When `true` (default), after a successful install the installer copies the bundled **`.polyinstall/tools/PolyInstall.Uninstall.exe`** to **`Uninstall.exe`** at the install root and registers the product in Add/Remove Programs. |

After every successful install or update, PolyInstall writes **`.polyinstall/install-state.json`** and **`.polyinstall/embedded-manifest.json`**. The state file records product identity, install location, version, and payload-owned files so future packaged installers for the same product can update in place and remove stale files that were installed by an earlier package.

**Elevation:** `install_scope: machine` writes to **HKLM** and requires an **elevated** (Administrator) install. On Windows, the installer relaunches itself with a UAC prompt before showing the wizard when machine scope is configured; use `user` scope for per-user installs under HKCU.

#### Linux build options

| Field | Meaning |
|--------|---------|
| `package` | `none` (default) or `appimage`. When `appimage`, the CLI builds an AppImage next to the raw ELF on **Linux** hosts (requires `mksquashfs` from **squashfs-tools**). |

#### macOS build options

| Field | Meaning |
|--------|---------|
| `package` | `none` (default) or `dmg`. When `dmg`, the CLI runs **`hdiutil`** to produce a compressed DMG beside the Mach-O binary. This step runs **only on macOS**. |

#### Installer signing

Signing is optional. When `build.signing` is omitted, PolyInstall produces unsigned artifacts exactly as the normal build pipeline does. When a platform signing block is present, the CLI validates that the required identity or certificate reference is configured, then signs the generated artifact after PolyInstall has appended the manifest and payload.

Do not put certificate passwords or Apple account credentials directly in YAML. Use certificate store references, keychains, notarytool keychain profiles, and environment-variable names for secrets.

```yaml
build:
  signing:
    windows:
      certificate_path: "${WINDOWS_CERT_PATH}"      # or certificate_thumbprint / certificate_subject
      certificate_password_env: WINDOWS_CERT_PASSWORD
      timestamp_url: "http://timestamp.digicert.com"
    macos:
      identity: "Developer ID Application: Example Corp"
      keychain: "${MACOS_KEYCHAIN_PATH}"
      notarization_profile: "polyinstall-notary"    # optional; requires build.macos.package: dmg
```

Windows signing uses `signtool` from `PATH` unless `tool_path` is configured. If Windows ARP registration is enabled, PolyInstall signs a temporary copy of `PolyInstall.Uninstall.exe` before embedding it, then signs the final generated installer `.exe`.

macOS signing uses `codesign` from `PATH` unless `codesign_path` is configured. For `build.macos.package: dmg`, PolyInstall signs the Mach-O installer before packaging, then signs the DMG; when `notarization_profile` is provided it also runs `xcrun notarytool submit --wait` and staples the ticket.

Linux signing is not built in. Linux outputs remain unsigned unless you run an external detached-signature workflow after the build.

### `ui`

| Field | Meaning |
|--------|---------|
| `theme` | `light`, `dark`, or `system`. |
| `logo_path` | Optional installer branding image shown in the wizard header; supports SVG and raster files resolved under the extracted payload unless absolute. |
| `assets` | Optional list of `{ id, path }` entries. AppImage packaging uses the first PNG asset as the application icon. |
| `wizard_steps` | Ordered steps for the Avalonia wizard (see [Wizard steps](#wizard-steps)). |

### `files`

A list of **glob groups**. Each entry has:

| Field | Meaning |
|--------|---------|
| `source_dir` | Root directory to search, relative to `--base`. |
| `include` | Glob patterns (e.g. `**/*`). |
| `exclude` | Optional exclude patterns. |

Matched files are stored in a **zip** inside the compressed payload, preserving paths relative to `source_dir`.

### `file_associations`

Optional list of file associations to register. Each entry has:

| Field | Meaning |
|-------|---------|
| `extension` | The file extension, including the leading dot (e.g., `.oef`). |
| `description` | A brief description of the file type. |
| `prog_id` | Optional: The ProgID for the file association (Windows, e.g., `MyApp.oef.1`). If omitted, one will be generated based on the application name and extension. |
| `icon` | Optional: The path to the icon file for this file type, relative to the install directory. |
| `command` | The command to execute when opening a file of this type. Use `%1` as a placeholder for the file path. |
| `mime_type` | Optional (Linux): The MIME type for this file association. If omitted, one will be derived from the extension (e.g., `.oef` → `application/x-oef`). |
| `bundle_path` | Required on macOS: Path to the `.app` bundle to register associations for. |

**Platform behavior:**
- **Windows**: Registers extension → ProgID mapping and ProgID → command in the registry.
- **Linux**: Writes MIME type XML to `~/.local/share/mime/packages/`, updates the `.desktop` entry, and sets the default handler via `xdg-mime`.
- **macOS**: Modifies the app bundle's `Info.plist` with `CFBundleDocumentTypes` and `UTExportedTypeDeclarations`, then re-registers with Launch Services.

These associations are registered during installation and restored or removed during uninstallation. Note: for more fine-grained control, you can also use the `file_association` task action.

### `services`

Optional list of background services/daemons to register after payload files are copied. Services are recorded in
`.polyinstall/install-state.json` so updates can remove stale registrations and uninstall can stop/disable/remove them
before deleting installed files.

| Field | Meaning |
|-------|---------|
| `name` | Service name. On macOS this is used as the launchd label; reverse-DNS names are recommended. |
| `require` | Required OS predicate such as `os.isWindows`, `os.isLinux`, or `os.isMacOS`. |
| `scope` | `system` or `user`. Windows supports `system` only. Linux/macOS support both. Defaults to `system`. |
| `enabled` | Whether the service is enabled for startup. Defaults to `true`. |
| `start` | Whether to start the service immediately after registration. Defaults to `false`. |
| `display_name` | Optional Windows display name. |
| `description` | Optional service description. |
| `executable` | Service executable path. Supports path placeholders. |
| `arguments` | Optional command-line arguments. |
| `working_directory` | Optional working directory. Supports path placeholders. |
| `restart` | Optional restart policy. Linux accepts systemd restart values; macOS maps `always` / `on-failure` to `KeepAlive`. |
| `environment` | Optional environment variables. |
| `features` | Optional feature ids that gate this service. |

**Platform behavior:**
- **Windows**: Uses the Service Control Manager via `sc.exe`. Services are machine-level and require Administrator rights; the installer relaunches elevated when needed.
- **Linux**: Writes systemd unit files under `/etc/systemd/system` for `scope: system` or `~/.config/systemd/user` for `scope: user`, then runs `systemctl`.
- **macOS**: Writes launchd plists under `/Library/LaunchDaemons` for `scope: system` or `~/Library/LaunchAgents` for `scope: user`, then runs `launchctl`.

`enabled` controls startup registration and defaults to `true`. `start` controls whether the service is started immediately
after registration and defaults to `false`. On uninstall, PolyInstall best-effort stops, disables, and removes every
service recorded in install state before deleting installed files.

```yaml
services:
  - name: "ExampleService"
    require: os.isWindows
    scope: system
    enabled: true
    start: false
    display_name: "Example Service"
    description: "MyApp background service"
    executable: "{AppDir}\\MyApp.exe"
    arguments: ["--service"]

  - name: "com.example.myapp"
    require: os.isLinux
    scope: user
    enabled: true
    start: false
    description: "MyApp background service"
    executable: "{AppDir}/bin/myapp"
    arguments: ["--service"]
    working_directory: "{AppDir}"
    restart: on-failure

  - name: "com.example.myapp"
    require: os.isMacOS
    scope: user
    enabled: true
    start: false
    description: "MyApp background service"
    executable: "{AppDir}/MyApp.app/Contents/MacOS/MyApp"
    arguments: ["--service"]
    working_directory: "{AppDir}"
    restart: always
```

### `features`

Optional list of installable features that the end user can toggle in the installer's
**features** wizard step.

| Field | Meaning |
|-------|---------|
| `id` | Unique identifier referenced by `files[].features`, `tasks.*[].features`, `file_associations[].features`, and `services[].features`. |
| `name` | Human-readable name shown next to the feature's checkbox. |
| `description` | Optional short description shown alongside the checkbox. |
| `default_selected` | When `true` (default), the feature is pre-checked on a fresh install. |

`files`, `tasks`, `file_associations`, and `services` entries without a `features:` list are **core**
— always installed regardless of selection. Entries that list one or more feature ids are
gated: they are installed/registered/executed only when at least one of the referenced
features is selected. The set of files that belongs to multiple features is allowed if
**any** of them is selected.

```yaml
features:
  - id: simulator
    name: Simulator
    description: Install the simulator runtime.
    default_selected: true
  - id: samples
    name: Samples
    default_selected: false

files:
  - source_dir: app
    include: ["bin/**/*"]               # core — always installed
  - source_dir: app
    include: ["sim/**/*"]
    features: [simulator]               # only installed if Simulator is selected

tasks:
  post_install:
    - action: create_shortcut
      require: os.isWindows
      features: [simulator]             # only runs if Simulator is selected
      parameters:
        target_path: "{AppDir}/sim.exe"
        name: "Simulator"
        location: start_menu
```

**Wizard step.** Add `type: features` between `destination` and `progress` to expose a
**Full install / Custom install** picker (full = all features; custom = per-feature
checkboxes). When the manifest has no `features:` list, the step is skipped automatically.

**Updates and uninstall.** `install-state.json` records the selected features. On update,
the installer pre-selects what was previously installed; deselecting a feature on an
update prunes its files and removes stale services. On uninstall, feature-gated tasks,
file associations, and services only run or clean up if the feature was installed.
Pre-feature installs (no `selected_features` recorded) fall back to running every
feature-gated task during uninstall.

**Backward compatibility.** A manifest without a `features:` list behaves exactly like
before this feature existed: every file/task/association/service is core and installed in full.

### `tasks`

Optional:

- `pre_install` — runs after the user confirms the destination but **before** files are copied (when using the default flow with a `progress` step).
- `post_install` — runs after files are copied.
- `pre_uninstall` — runs at the start of uninstall (Windows), before registry removal and file deletion.
- `post_uninstall` — runs after `pre_uninstall`, before Add/Remove Programs removal and tree deletion.

Each task supports:

| Field | Meaning |
|--------|---------|
| `require` | Optional condition string (see [Conditions](#conditions)). If omitted, the task always runs when the phase runs. |
| `action` | One of the supported actions (see [Task actions](#task-actions)). |
| `parameters` | Key/value map; keys are snake_case strings matching the action. **String** values support the same [path placeholders](#path-placeholders) as wizard steps (expanded when the task runs). |

---

## Environment variable substitution (CLI only)

During **`build`** and **`validate`**, all **string values** in the manifest (after YAML → model → JSON) are processed for:

- `${VAR}` — replaced from the process environment or from extra keys you add in code later.
- `${VAR:-default}` — if `VAR` is unset or empty, use `default`.

This is intended for **CI and local paths**, not for security-sensitive runtime secrets in the shipped JSON (the embedded manifest is visible inside the binary).



## CLI reference

**Release builds:** run `polyinstall` or `polyinstall.exe` from the extracted [GitHub Release](https://github.com/bolorundurowb/PolyInstall/releases) zip (self-contained; no `dotnet` prefix).

**From source:** invoke via `dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj --` or run the built
`polyinstall.dll` with `dotnet polyinstall.dll`.

```text
polyinstall build <manifest.yaml> [--base <dir>] [--stubs <dir>] [--verbose] [--json] [--output-manifest <file>]
polyinstall validate <manifest.yaml> [--base <dir>]
```

| Command | Purpose |
|---------|---------|
| **`build`** | Full pipeline: read YAML → substitute env vars → validate JSON Schema → glob → zip → compress → append to each stub for each `build.targets` entry → write outputs → optional AppImage (Linux) or DMG (macOS) per manifest. |
| **`validate`** | Same parse, substitution, and schema validation as `build`, without producing binaries. |

| Option | Purpose |
|--------|---------|
| **`--base`** | Working directory used to resolve `files[].source_dir` and default `output_dir`. Defaults to the manifest file’s directory. |
| **`--stubs`** | Root folder containing per-RID stub directories. When omitted: `stubs` next to the `polyinstall` executable if that directory exists, otherwise `<base>/stubs`. |
| **`--verbose`** | Emit detailed build progress messages. |
| **`--json`** | After a successful build, emit a JSON manifest to **stdout** listing every produced artifact. When this flag is present, normal build logs are suppressed so the output is machine-parseable. |
| **`--output-manifest`** | After a successful build, write the same JSON artifact manifest to the specified file path. Can be combined with `--json`. |

The CLI loads `schema/v1.json` from next to the built CLI assembly, walks upward from the CLI assembly directory, then falls back to `schema/v1.json` under the current working directory.



## Build targets

Manifest `build.targets` entries use **tokens** that map to .NET RIDs:

| Manifest token | .NET RID |
|----------------|----------|
| `windows-x64` | `win-x64` |
| `windows-arm64` | `win-arm64` |
| `linux-x64` | `linux-x64` |
| `linux-arm64` | `linux-arm64` |
| `osx-x64` | `osx-x64` |
| `osx-arm64` | `osx-arm64` |

Official CLI zips include a bundled `stubs/` directory for that zip's host RID. For other target RIDs, download the
matching `stubs-<rid>-<tag>.zip` release asset or use the commands below to **publish stubs yourself** (from-source
workflow or custom RIDs).

For each token you support outside bundled stubs, publish the runtime once:

```bash
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r linux-x64 -o stubs/linux-x64
```

Use the same folder layout the CLI expects (`stubs/<rid>/PolyInstall.Runtime...`, and on Windows with `register_arp: true` also `stubs/<rid>/PolyInstall.Uninstall.exe`), or set `build.stub_path`.



## Compression

The payload is **zip** bytes, then **compressed** as configured:

| `build.compression` | Behaviour |
|---------------------|----------|
| **`brotli`** | Brotli compression (recommended default). |
| **`gzip`** | GZip compression. |

The stub reads `build.compression` from the embedded JSON and decompresses accordingly.



## Windows uninstall and ARP

When `build.windows.register_arp` is true (default), a successful install:

1. Writes **`.polyinstall/embedded-manifest.json`** (full manifest JSON) and **`.polyinstall/install-state.json`** (product id, paths, registry key path).
2. Copies **`.polyinstall/tools/PolyInstall.Uninstall.exe`** (placed in the payload at build time from your stubs folder) to **`Uninstall.exe`** in the install directory.
3. Registers **Add/Remove Programs** under `HKCU` or `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\{GUID}` depending on `install_scope`.

**Uninstall:** From the install directory, run **`Uninstall.exe --uninstall`** (add **`--quiet`** to skip the confirmation prompt). The uninstall host is **not** the installer stub: it has **no** appended payload; it reads **`.polyinstall/install-state.json`** and **`embedded-manifest.json`** from disk, runs **`pre_uninstall`** / **`post_uninstall`** tasks, removes the ARP key, deletes installed files, and schedules removal of the install folder (including **`Uninstall.exe`** after the process exits).

**Alternate path:** From any directory you can run **`.polyinstall/tools/PolyInstall.Uninstall.exe`** (or a copy) with **`--uninstall --install-location "C:\Path\To\Install"`**. The self-extracting **installer** binary does **not** implement **`--uninstall`**; use **`Uninstall.exe`** or the bundled uninstall tool as above.



## Linux AppImage

Set `build.linux.package: appimage` for `linux-*` targets. After the usual ELF bundle is produced, the CLI:

1. Assembles an **AppDir** (`AppRun`, `.desktop`, `usr/bin/<installer>`).
2. Invokes **`mksquashfs`** (from **squashfs-tools**) to build a squashfs image.
3. Prepends the **AppImage type-2 runtime** ELF (downloaded once from AppImageKit and cached under your temp directory) and marks the result executable.

**Host requirement:** AppImage creation must run on **Linux** with `mksquashfs` on `PATH`. Windows and macOS hosts cannot produce AppImages with this pipeline.



## macOS DMG

Set `build.macos.package: dmg` for `osx-*` targets. After the Mach-O bundle is built, the CLI stages it in a temp folder (optional **Applications** symlink for drag-to-Applications UX) and runs **`hdiutil create`** to emit a **UDZO** DMG next to the binary.

**Host requirement:** DMG creation must run on **macOS** (`hdiutil` is not available on Linux/Windows CI for this purpose).



## Bundle layout (append model)

The CLI **does not** rebuild the stub per package. It copies the stub bytes, then appends:

1. UTF-8 JSON manifest
2. Compressed payload bytes
3. A **20-byte footer**: payload length (8 LE), manifest length (4 LE), magic `POLYIN01` (8 bytes)

The stub opens its own executable path, finds the PolyInstall footer, and reads manifest + payload. It checks the physical end first, then scans backward so signed artifacts still work when signing tools append signature data after the PolyInstall footer.



## Wizard steps

`ui.wizard_steps` is a list of steps. Each step has a **`type`** and optional fields:

| `type` | Typical fields | Behaviour |
|--------|----------------|----------|
| `welcome` | `title` | Introduction text. |
| `eula` | `title`, `source` | Loads licence text from `source` (path under extracted payload or absolute). |
| `destination` | `title`, `default_path` | User chooses install directory; placeholders expanded (see below). If omitted on Windows, machine-scope installs default under Program Files and user-scope installs default under LocalAppData. |
| `features` | `title` | Full/Custom picker for optional `features:` defined in the manifest. Auto-skipped when the manifest has no features. Must sit between `destination` and `progress`. |
| `progress` | `title` | Runs **pre-install** tasks, copies extracted payload to the install directory, then **post-install** tasks. |
| `finish` | `title` | Summary. |

If `wizard_steps` is empty, the UI falls back to a minimal welcome + finish flow.



## Path placeholders

Wizard strings (for example `ui.wizard_steps` → `destination.default_path`), **task string parameters**, and **service string fields** (`executable`, `arguments`, `working_directory`, and `environment` values) can include:

| Placeholder | Meaning |
|-------------|---------|
| `{AppDir}` | Install directory: the PAL’s `AppDir` when it is non-empty; otherwise the chosen install directory or extract root (same idea as the live installer host). |
| `{ProgramFiles}` | OS-appropriate program files location. |
| `{UserHome}` | Current user’s profile/home. |
| `{Desktop}` | Desktop folder. |

**Wizard UI** normalizes slashes using the manifest’s `build.targets` / installer-target hint when present, so a path typed on a build machine can match the target OS.

**Tasks** always run on the **machine executing the installer**; placeholder expansion uses that host’s rules for directory separators (not the cross-build RID alone), so shortcuts and registry values match the OS where the install actually runs.



## Conditions

`tasks[].require` supports a fixed set of predicates (case-insensitive; underscores optional):

- `os.isWindows` / `os.is_windows`
- `os.isLinux` / `os.is_linux`
- `os.isOSX` / `os.is_osx` / `os.isMacOS` / `os.is_macos`
- `os.isUnix` / `os.is_unix` (Linux or macOS)

Unknown expressions throw at runtime — there is **no** general-purpose expression language by design.



## Task actions

String parameter values are passed through [path placeholder](#path-placeholders) expansion before the action runs (except `value_kind`, which is interpreted as a registry kind token only).

| `action` | Platform | `parameters` (keys) |
|----------|----------|----------------------|
| `create_shortcut` | Windows: `.lnk` via PowerShell; Linux/macOS: symlink or shell wrapper | `target_path`, `name`, `location` (`start_menu` or `desktop`), optional `subfolder`, optional `description`, `icon_path` |
| `write_registry` | Windows only | `key_path` (e.g. `HKCU\Software\Vendor\App`), `value_name`, `value`, `value_kind` (`string`, `reg_sz`, `dword`, …) |
| `create_desktop_entry` | Linux only (Freedesktop-style) | `file_name`, `name`, `exec`, optional `icon`, `comment` |
| `set_permissions` | Linux / macOS | `path`, `mode` (integer, e.g. octal `755` as decimal or use the value your pipeline expects — the PAL passes through to `chmod`) |
| `file_association` | All platforms | `extension`, `description`, optional `prog_id`, optional `icon`, `command`, optional `mime_type` (Linux), optional `bundle_path` (macOS, required) |

If an action is not supported on the current OS, the runtime throws a clear **platform not supported** error for that task.

### `create_shortcut` examples

On **Windows**, `name` should **not** include `.lnk` — PolyInstall appends it automatically when invoking `WScript.Shell`.

```yaml
tasks:
  post_install:
    - action: create_shortcut
      require: os.isWindows
      parameters:
        target_path: "{AppDir}\\MyApp.exe"
        name: "MyApp"                # Do NOT add .lnk
        location: start_menu          # or desktop
        subfolder: "MyVendor"         # optional
        description: "My Application"
        icon_path: "{AppDir}\\MyApp.ico"
```

On **Linux/macOS**, `create_shortcut` creates a symlink or shell wrapper. The `.lnk` rule does not apply.

```yaml
tasks:
  post_install:
    - action: create_shortcut
      require: os.isLinux
      parameters:
        target_path: "{AppDir}/bin/myapp"
        name: "MyApp"
        location: desktop
```

### `file_association` examples

Always add a `require` predicate so the task only runs on the intended platform. Use `%1` as the placeholder for the file path.

```yaml
tasks:
  post_install:
    - action: file_association
      require: os.isWindows
      parameters:
        extension: ".oef"
        description: "Open Exam File"
        command: "{AppDir}\\MyApp.exe %1"
    - action: file_association
      require: os.isLinux
      parameters:
        extension: ".oef"
        description: "Open Exam File"
        command: "myapp %1"
        mime_type: "application/x-oef"
    - action: file_association
      require: os.isMacOS
      parameters:
        extension: ".oef"
        description: "Open Exam File"
        command: "open -a MyApp %1"
        bundle_path: "{AppDir}/MyApp.app"
```



## Output file naming

Under `output_dir`, the CLI writes:

```text
<sanitized-metadata-name>-<manifest-target-token>.exe   # Windows
<sanitized-metadata-name>-<manifest-target-token>       # non-Windows
```

Invalid file-name characters in `metadata.name` are replaced with underscores.



## Troubleshooting

| Symptom | What to check |
|---------|----------------|
| **Stub not found** | Publish `PolyInstall.Runtime` under `<rid>/` in the resolved stubs root (`--stubs`, or `stubs` next to `polyinstall`, or `<base>/stubs`). On Windows targets with `register_arp: true`, also publish `PolyInstall.Uninstall.exe` there, or set `build.stub_path` with `{rid}`. |
| **Schema validation errors** | Run `polyinstall validate`; ensure YAML keys are snake_case and match `schema/v1.json`. |
| **No files matched** | Check `source_dir` relative to `--base`, and `include` / `exclude` patterns. |
| **Wrong OS** | The stub RID must match the machine (e.g. do not run a `win-x64` build on Linux). |
| **Shortcut / registry tasks fail on Windows** | PowerShell execution policy, permissions, and paths in `parameters`. |
| **Service registration fails on Windows** | Windows services require Administrator rights. Confirm the installer elevated successfully and that `sc.exe` is available. |
| **Service registration fails on Linux** | Confirm `systemctl` exists. `scope: system` requires root; `scope: user` requires a usable user systemd session. |
| **Service registration fails on macOS** | Confirm `launchctl` is available. `scope: system` requires root and writes `/Library/LaunchDaemons`; `scope: user` writes `~/Library/LaunchAgents`. |



## Examples

- **`examples/polyinstall.sample.yaml`** — Minimal end-to-end manifest.
- **`examples/sample-payload/`** — Tiny payload tree for testing globs.



## CI Examples (GitHub Actions)

This workflow downloads a pre-built `polyinstall` release, builds an installer, and uses `--json` to discover the artifact path for a release upload.

```yaml
name: Build Installer

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Download polyinstall CLI
        run: |
          curl -L -o polyinstall.zip \
            "https://github.com/${{ github.repository }}/releases/download/v1.0.0/polyinstall-linux-x64-v1.0.0.zip"
          unzip polyinstall.zip
          chmod +x polyinstall-linux-x64/polyinstall

      - name: Build installer
        run: |
          ./polyinstall-linux-x64/polyinstall build \
            manifest.yaml \
            --base . \
            --json \
            --output-manifest build-manifest.json

      - name: Upload installer artifact
        uses: actions/upload-artifact@v6
        with:
          name: installer
          path: |
            ${{ fromJson(steps.build.outputs.manifest).artifacts[0].path }}
```

> **Tip:** Pipe `--json` output directly to `jq` to extract paths in shell scripts:
> ```bash
> ./polyinstall build manifest.yaml --json | jq -r '.artifacts[0].path'
> ```



## Third-party notices

See **`THIRD_PARTY_NOTICES.txt`** in the repository for NuGet components used by the CLI, UI, and libraries.

## Relationship to this repository

**Prefer [GitHub Releases](https://github.com/bolorundurowb/PolyInstall/releases):** download the zip for your host OS
and use the bundled `polyinstall` and `stubs/` instead of building this repository from source.

Only if you need unreleased behaviour, private forks, or custom stub layouts should you **vendor** this repository (or a
fork) and run `dotnet publish` / `dotnet run` from source as described in [Quick start](#quick-start) and
[CONTRIBUTING.md](CONTRIBUTING.md).

If you embed PolyInstall into your own product, keep the **schema version** (`schema/v1.json`), **installer stub**, and (when Windows ARP registration is enabled) **`PolyInstall.Uninstall`** outputs in sync — mismatches between the CLI bundle format and an older stub or uninstall host can fail at the magic/footer check, during decompression, or when the bundled uninstall path is missing.

For development and pull request guidance, see [CONTRIBUTING.md](CONTRIBUTING.md).
