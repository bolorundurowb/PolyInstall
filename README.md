# PolyInstall

PolyInstall is a **.NET-centric toolchain** for building **self-contained installer executables** from a **YAML manifest**. At build time, the CLI packs your application files, compresses them, and appends them (with an embedded JSON manifest) to a **pre-published stub** — a small Avalonia-based host that extracts the payload and walks the end user through an installer wizard.

This document is written for **consumers**: teams who want to ship installers without adopting a separate installer product, and who are comfortable with YAML, the .NET CLI, and publishing self-contained or framework-dependent apps per runtime identifier (RID).

---

## What you get

| Piece | Role |
|--------|------|
| **`polyinstall` CLI** | Parses YAML, substitutes environment variables, validates against JSON Schema, globs files, builds a zip payload, compresses it, and produces one output binary per `build.targets` entry. |
| **Stub (`PolyInstall.Runtime`)** | The actual “installer.exe” you ship. It reads the bundle appended to itself, shows a wizard (`PolyInstall.UI`), copies files, and can run **tasks** (shortcuts, registry, `.desktop` files, permissions). |
| **`schema/v1.json`** | JSON Schema generated from the same C# models as the runtime. Use it in your editor for completion and diagnostics (see [Manifest and schema](#manifest-and-schema)). |

**Not included (yet):** platform-specific packaging such as MSI, DMG, signed macOS bundles, `.deb`, or AppImage. The current model is **portable per-RID executables** with an appended payload (Phase A–style delivery).

---

## Requirements

- **.NET SDK 10** (or the version aligned with `Directory.Build.props` / `TargetFramework` in this repo).
- A **64-bit** target OS matching the stubs you publish (Windows, Linux, or macOS RIDs supported in the manifest; see [Build targets](#build-targets)).
- **Windows** stubs: PowerShell available for shortcut creation when using `create_shortcut` tasks (COM via `WScript.Shell`).

---

## Quick start

1. **Clone or copy** this repository (or consume the published packages / tool if you publish them yourself).

2. **Create a manifest** — start from `examples/polyinstall.sample.yaml` and adjust `metadata`, `files`, and `build.targets`.

3. **Publish the stub** for each RID you need (example: Windows x64):

   ```bash
   dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r win-x64 -o stubs/win-x64
   ```

   The CLI looks for `PolyInstall.Runtime.exe` (Windows) or `PolyInstall.Runtime` (non-Windows) under `<stubs>/<rid>/` by default.

4. **Build the installer**:

   ```bash
   dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- build examples/polyinstall.sample.yaml --base examples --stubs stubs
   ```

   Outputs appear under `build.output_dir` from the manifest (relative to `--base`), e.g. `examples/dist/SampleApp-windows-x64.exe`.

5. **Run the produced `.exe`** (or non-Windows binary) on a matching OS. It will extract to a temp folder and launch the wizard.

---

## Manifest and schema

- **Authoring format:** YAML with **snake_case** keys (see examples).
- **Runtime format:** The stub reads an **embedded JSON** manifest (snake_case property names) produced by the CLI.
- **Schema for editors:** After building the repo, `schema/v1.json` is the source of truth. To regenerate it from the C# models:

  ```bash
  dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj
  ```

  Optional MSBuild integration: pass **`/p:GeneratePolyInstallSchema=true`** so `Directory.Build.targets` runs the generator before compile (opt-in).

**IDE integration:** Point your YAML at the schema for validation and IntelliSense, for example:

```yaml
# yaml-language-server: $schema=https://polyinstall.dev/schema/v1.json
```

If you work offline, use a **relative** or `file:` URL to `schema/v1.json` in your clone.

---

## Manifest structure (five domains)

The manifest is grouped into five sections. All are represented in JSON Schema; only the fields you need must be set (defaults apply where defined in code).

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
| `stub_path` | Optional path to the stub for a target; use `{rid}` for the **.NET RID** (e.g. `C:\stubs\{rid}\PolyInstall.Runtime.exe`). If omitted, the CLI uses `--stubs/<rid>/PolyInstall.Runtime[.exe]`. |

### `ui`

| Field | Meaning |
|--------|---------|
| `theme` | `light`, `dark`, or `system`. |
| `assets` | Optional list of `{ id, path }` entries for future asset wiring (paths resolved under the extracted payload). |
| `wizard_steps` | Ordered steps for the Avalonia wizard (see [Wizard steps](#wizard-steps)). |

### `files`

A list of **glob groups**. Each entry has:

| Field | Meaning |
|--------|---------|
| `source_dir` | Root directory to search, relative to `--base`. |
| `include` | Glob patterns (e.g. `**/*`). |
| `exclude` | Optional exclude patterns. |

Matched files are stored in a **zip** inside the compressed payload, preserving paths relative to `source_dir`.

### `tasks`

Optional:

- `pre_install` — runs after the user confirms the destination but **before** files are copied (when using the default flow with a `progress` step).
- `post_install` — runs after files are copied.

Each task supports:

| Field | Meaning |
|--------|---------|
| `require` | Optional condition string (see [Conditions](#conditions)). If omitted, the task always runs when the phase runs. |
| `action` | One of the supported actions (see [Task actions](#task-actions)). |
| `parameters` | Key/value map; keys are snake_case strings matching the action. |

---

## Environment variable substitution (CLI only)

During **`build`** and **`validate`**, all **string values** in the manifest (after YAML → model → JSON) are processed for:

- `${VAR}` — replaced from the process environment or from extra keys you add in code later.
- `${VAR:-default}` — if `VAR` is unset or empty, use `default`.

This is intended for **CI and local paths**, not for security-sensitive runtime secrets in the shipped JSON (the embedded manifest is visible inside the binary).

---

## CLI reference

Invoke the CLI via `dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj --` or by running the built `polyinstall.dll` with `dotnet polyinstall.dll`.

```text
polyinstall build <manifest.yaml> [--base <dir>] [--stubs <dir>]
polyinstall validate <manifest.yaml> [--base <dir>]
```

| Command | Purpose |
|---------|---------|
| **`build`** | Full pipeline: read YAML → substitute env vars → validate JSON Schema → glob → zip → compress → append to each stub for each `build.targets` entry → write outputs. |
| **`validate`** | Same parse, substitution, and schema validation as `build`, without producing binaries. |

| Option | Purpose |
|--------|---------|
| **`--base`** | Working directory used to resolve `files[].source_dir` and default `output_dir`. Defaults to the manifest file’s directory. |
| **`--stubs`** | Root folder containing per-RID stub directories. Defaults to `<base>/stubs`. |

The CLI loads `schema/v1.json` from next to the built CLI assembly, or walks upward from the current base path to find `schema/v1.json`.

---

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

For each token, publish the runtime once:

```bash
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj -c Release -r linux-x64 -o stubs/linux-x64
```

Use the same folder layout the CLI expects (`stubs/<rid>/PolyInstall.Runtime...`), or set `build.stub_path`.

---

## Compression

The payload is **zip** bytes, then **compressed** as configured:

| `build.compression` | Behavior |
|---------------------|----------|
| **`brotli`** | Brotli compression (recommended default). |
| **`gzip`** | GZip compression. |

The stub reads `build.compression` from the embedded JSON and decompresses accordingly.

---

## Bundle layout (append model)

The CLI **does not** rebuild the stub per package. It copies the stub bytes, then appends:

1. UTF-8 JSON manifest  
2. Compressed payload bytes  
3. A **20-byte footer**: payload length (8 LE), manifest length (4 LE), magic `POLYIN01` (8 bytes)

The stub opens its own executable path, seeks to the end, validates the magic, and reads manifest + payload. You can re-sign the final binary with your own pipeline if you add signing **after** this append step (signing details are outside this README).

---

## Wizard steps

`ui.wizard_steps` is a list of steps. Each step has a **`type`** and optional fields:

| `type` | Typical fields | Behavior |
|--------|----------------|----------|
| `welcome` | `title` | Introduction text. |
| `eula` | `title`, `source` | Loads license text from `source` (path under extracted payload or absolute). |
| `destination` | `title`, `default_path` | User chooses install directory; placeholders expanded (see below). |
| `progress` | `title` | Runs **pre-install** tasks, copies extracted payload to the install directory, then **post-install** tasks. |
| `finish` | `title` | Summary. |

If `wizard_steps` is empty, the UI falls back to a minimal welcome + finish flow.

---

## Path placeholders

Strings such as `default_path` can include:

| Placeholder | Meaning |
|-------------|---------|
| `{AppDir}` | Chosen install directory (or extract root until a destination is chosen). |
| `{ProgramFiles}` | OS-appropriate program files location. |
| `{UserHome}` | Current user’s profile/home. |
| `{Desktop}` | Desktop folder. |

---

## Conditions

`tasks[].require` supports a fixed set of predicates (case-insensitive; underscores optional):

- `os.isWindows` / `os.is_windows`
- `os.isLinux` / `os.is_linux`
- `os.isOSX` / `os.is_osx` / `os.is_macos` / `os.is_macos`
- `os.isUnix` / `os.is_unix` (Linux, macOS, or FreeBSD)

Unknown expressions throw at runtime — there is **no** general-purpose expression language by design.

---

## Task actions

| `action` | Platform | `parameters` (keys) |
|----------|----------|----------------------|
| `create_shortcut` | Windows: `.lnk` via PowerShell; Linux/macOS: symlink or shell wrapper | `target_path`, `shortcut_path`, optional `description`, `icon_path` |
| `write_registry` | Windows only | `key_path` (e.g. `HKCU\Software\Vendor\App`), `value_name`, `value`, `value_kind` (`string`, `reg_sz`, `dword`, …) |
| `create_desktop_entry` | Linux / macOS (Freedesktop-style) | `file_name`, `name`, `exec`, optional `icon`, `comment` |
| `set_permissions` | Unix | `path`, `mode` (integer, e.g. octal `755` as decimal or use the value your pipeline expects — the PAL passes through to `chmod`) |

If an action is not supported on the current OS, the runtime throws a clear **platform not supported** error for that task.

---

## Output file naming

Under `output_dir`, the CLI writes:

```text
<sanitized-metadata-name>-<manifest-target-token>.exe   # Windows
<sanitized-metadata-name>-<manifest-target-token>       # non-Windows
```

Invalid file-name characters in `metadata.name` are replaced with underscores.

---

## Troubleshooting

| Symptom | What to check |
|---------|----------------|
| **Stub not found** | Publish `PolyInstall.Runtime` to `--stubs/<rid>/` or set `build.stub_path` with `{rid}`. |
| **Schema validation errors** | Run `polyinstall validate`; ensure YAML keys are snake_case and match `schema/v1.json`. |
| **No files matched** | Check `source_dir` relative to `--base`, and `include` / `exclude` patterns. |
| **Wrong OS** | The stub RID must match the machine (e.g. do not run a `win-x64` build on Linux). |
| **Shortcut / registry tasks fail on Windows** | PowerShell execution policy, permissions, and paths in `parameters`. |

---

## Examples

- **`examples/polyinstall.sample.yaml`** — Minimal end-to-end manifest.  
- **`examples/sample-payload/`** — Tiny payload tree for testing globs.

---

## Continuous integration and releases

GitHub Actions workflows live under **`.github/workflows/`**:

| Workflow | When | What |
|----------|------|------|
| **`ci.yml`** | Push to `master`, or pull requests targeting `master` | Restores, builds, runs tests with **XPlat Code Coverage**, uploads Cobertura to [**Codecov**](https://codecov.io). |
| **`release.yml`** | Push of a tag matching **`v*`** (e.g. `v1.2.0`) | Runs tests, publishes a **self-contained single-file** `polyinstall` CLI for `win-x64`, `linux-x64`, and `osx-arm64`, and creates a **GitHub Release** with those zip assets and auto-generated release notes. |

**Codecov:** Add a repository secret **`CODECOV_TOKEN`** from your Codecov project settings (recommended). Many **public** repositories can upload without a token; see [Codecov’s GitHub docs](https://docs.codecov.com/docs/github-2). Coverage behavior is configured in **`codecov.yml`**.

**Releases:** Create a tag locally and push it, for example:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Pre-release tags that include a hyphen in the version segment (e.g. `v1.0.0-beta.1`) are marked as **pre-release** on GitHub.

---

## Third-party notices

See **`THIRD_PARTY_NOTICES.txt`** in the repository for NuGet components used by the CLI, UI, and libraries.

---

## Relationship to this repository

Consumers typically:

1. Depend on a **released** `polyinstall` tool and/or packages, **or**  
2. **Vendor** this repository (or a fork) and run `dotnet publish` / `dotnet run` from source as shown above.

If you embed PolyInstall into your own product, keep the **schema version** (`schema/v1.json`) and **stub version** in sync — mismatches between CLI bundle format and an older stub will fail at the magic/footer check or during decompression.

For **contributors** and architecture notes, see `.cursor/plans/` and the source under `src/PolyInstall.*`.
