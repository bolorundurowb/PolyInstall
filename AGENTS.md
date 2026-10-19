# AGENTS.md — PolyInstall

Guidance for humans and AI agents contributing code to PolyInstall. This file is **normative**: it describes the
architecture we want, not always the architecture we have today. When existing code conflicts with this document, prefer
the rules here for new work and refactor the offending code when you are already touching it.

Everything human-readable in this repository — docs, comments, diagnostics, UI copy, commits — is authored in Nigerian
English (`en-NG`). See §4.

Read [ARCHITECTURE.md](ARCHITECTURE.md) for how the system works and [CONTRIBUTING.md](CONTRIBUTING.md) for workflow.

---

## 1. Product mission (non-negotiable)

PolyInstall is a **manifest-driven, cross-platform installer generator**. Three constraints shape every decision:

1. **It is not a .NET tool that happens to run elsewhere — it is a general-purpose packaging tool.**
   - The manifest (YAML) is the *only* user-facing interface. Any payload that can be put in a folder can be packaged:
     Node apps, Python apps, Go/Rust binaries, Java jars, Electron apps, static sites, scripts, game assets.
   - **Never** introduce a feature that assumes or requires the payload to be .NET. No `<runtimeconfig>` inference, no
     assembly probing, no `dotnet` invocation on behalf of the payload, no `deps.json` awareness, no MSBuild coupling.
   - **Never** require end users to have the .NET SDK or runtime. Installers must run on clean machines; stubs are
     self-contained (`PublishSingleFile`, `SelfContained`) for exactly this reason. The CLI ships self-contained too.
   - Describing anything in docs, diagnostics, or UI as ".NET-only" or requiring `dotnet` (outside of *our own*
     contributor build instructions) is a bug.
2. **Windows, Linux, and macOS are first-class, simultaneously.**
   - A feature that only works on one OS is a *partial* feature: it must be expressed in the manifest (ideally behind a
     `require:` condition), implemented behind the PAL, degrade gracefully elsewhere, and be documented as
     platform-scoped.
   - Feature parity is measured per capability (shortcuts, PATH, services, associations, permissions, UI), not per line
     of code shipped.
3. **Lightweight above all.** Small installers, small stubs, small CLI, fast cold start, minimal dependencies, no
   write-amplification on the target machine. Size and dependency regressions are breaking changes in spirit — see §7.

If a change improves developer convenience but violates one of the three constraints above, it is rejected.

---

## 2. Repository map and dependency law

```text
src/
  PolyInstall.Core           # shared models, bundle format, coordinators, PAL contracts  (no YAML, no Avalonia)
  PolyInstall.Core.Build     # YAML parsing, globbing, semantic validation               (build time only)
  PolyInstall.Cli            # `polyinstall` CLI: build / validate / package / sign
  PolyInstall.Runtime        # published wizard installer stub (Avalonia)
  PolyInstall.Runtime.Silent # published UI-less installer stub (ui.mode: silent)
  PolyInstall.Uninstall      # Windows uninstall host (no appended bundle)
  PolyInstall.UI             # Avalonia wizard
  PolyInstall.SchemaGen      # generates schema/v1.json from Core manifest models
  tests/…                    # xUnit + OmniAssert
schema/v1.json               # generated; committed; served to users via the docs site
examples/                    # sample manifest + sample payload
```

**The dependency law (enforce this, it is what keeps installers small):**

```text
Cli ──► Core.Build ──► Core
Runtime ──► UI ──► Core
Runtime.Silent ──► Core
Uninstall ──► Core
SchemaGen ──► Core
```

- **`PolyInstall.Core` must stay dependency-free.** Adding a NuGet package to Core is the single most expensive change
  possible here: it ships in every installer the world will ever build. Treat every Core dependency as a
  release-blocking decision requiring explicit justification in the PR description.
- Anything only needed to *author* an installer belongs in `Core.Build` or `Cli` (YAML parsers, globbers, schema
  validators, packaging tools).
- Core must never reference UI, Cli, Runtime, or Uninstall. UI must never reference Cli/Core.Build.
- OS-specific code lives **only** behind PAL interfaces in `Core/Pal/`. A `RuntimeInformation.IsOSPlatform(...)` or
  `#if` in `Install/`, `Payload/`, `Manifest/`, or `UI/` is a design error; put it in a PAL implementation.

**Direction of truth:** manifest models (`Core/Manifest/`) are the single source of truth for the manifest shape. JSON
schema is *generated* from them. YAML keys are snake_case; C# properties are PascalCase; serialization is done through
`InstallManifest.JsonOptions`.

---

## 3. Commands

```bash
dotnet restore src/PolyInstall.slnx
dotnet build   src/PolyInstall.slnx -c Release --no-restore
dotnet test    src/PolyInstall.slnx -c Release --no-build --verbosity normal
dotnet test    src/PolyInstall.slnx -c Release --no-build /p:CollectCoverage=true   # Cobertura → src/coverage/<proj>/

# Regenerate schema/v1.json after ANY manifest model or validation change (commit the result)
dotnet run --project src/PolyInstall.SchemaGen/PolyInstall.SchemaGen.csproj

# Build stubs for local installer smoke tests
dotnet publish src/PolyInstall.Runtime/PolyInstall.Runtime.csproj        -c Release -r win-x64 -o stubs/win-x64
dotnet publish src/PolyInstall.Runtime.Silent/PolyInstall.Runtime.Silent.csproj -c Release -r win-x64 -o stubs/win-x64
dotnet publish src/PolyInstall.Uninstall/PolyInstall.Uninstall.csproj    -c Release -r win-x64 -o stubs/win-x64   # Windows + register_arp

# End-to-end smoke loop
dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- build examples/polyinstall.sample.yaml --base examples --stubs stubs
dotnet run --project src/PolyInstall.Cli/PolyInstall.Cli.csproj -- validate examples/polyinstall.sample.yaml --base examples
```

There is no linter/format pipeline beyond the compiler. `dotnet build` must emit **no new warnings**; treat every new
warning as review feedback. (`TreatWarningsAsErrors` is off today — do not rely on that as licence to add warnings.)

---

## 4. Language and locale: en-NG

**Every human-readable surface of this project is authored in Nigerian English (`en-NG`).** That covers the README,
ARCHITECTURE.md, CONTRIBUTING.md, the docs site (`index.html` on the `gh-pages` branch), comments in
`examples/polyinstall.sample.yaml`, code comments, XML doc comments, CLI output, validator diagnostics, UI copy, commit
messages, and PR descriptions.

- **Spelling follows Nigerian/British conventions:** `behaviour`, `colour`, `centre`, `metre`, `cancelled`,
  `labelled`, `modelling`, `analysing`, `licence` (noun) vs `license` (verb), `practice` (noun) vs `practise` (verb),
  `prioritise`, `recognise`, `optimise`, `initialise`, `organise`, `customise`, `summarise`, `minimise`,
  `authorisation`, `preferred`. Keep US-only forms (`behavior`, `color`, `center`, `canceled`, `labeled`, `optimize`,
  `authorization`) out of anything a person reads.
- **Frozen names are never renamed for spelling.** These are compatibility contracts (§6) and keep their spelling until
  a documented migration removes them: manifest keys and JSON members (e.g. `notarization_profile`, `compression`,
  `output_name`), file names under `.polyinstall/`, the bundle trailer magic, CLI flags, any identifier that mirrors a
  third-party API (`Serialize`, `Normalize`, Avalonia `Color`, .NET `Canceled`), vendor/service names as the vendor
  spells them (`notarization` — Apple's own term), and every already-published type or
  member name (`NormalizeRelativePath`, `ManifestSemanticValidator`). Code identifiers follow .NET ecosystem spelling;
  `en-NG` governs *narrative* text. A spelling-driven rename is churn that breaks consumers — do not do it.
- **Dates, times, and numbers in prose:** ISO 8601 (`2026-03-14`) or `dd MMM yyyy` (`14 Mar 2026`); never
  `MM/DD/YYYY`. Times use the 24-hour clock (`18:45`). Money examples use `₦`/NGN, not `$`.
- **Machine-facing formats are culture-independent, always.** Anything written to the bundle, `install-state.json`,
  `embedded-manifest.json`, build logs, `--output-manifest`, or parsed back later must use
  `CultureInfo.InvariantCulture` (or the ISO round-trip format `"O"`) and ordinal comparisons. A bundle produced under
  `en-US` must read identically under `en-NG`, `de-DE`, or `ar-EG`; never let ambient culture leak into a persisted or
  parsed value.
- **Stubs run with `InvariantGlobalization=true`** (`PolyInstall.Runtime`, `PolyInstall.Uninstall`) to keep size down
  (§7). Do not "fix" that by adding ICU packages or `.resx` satellites for locale formatting: format explicitly and keep
  UI copy as plain English strings. A localization framework in Core/UI is a size regression and needs §7 sign-off.
- **Tests must not depend on ambient culture.** Assert with invariant culture or set `CultureInfo.CurrentCulture` in a
  fixture and restore it. A test that only passes on an `en-US` host is a failing test.
- **No region-locked language.** Avoid idioms, sports metaphors, and US-only civic or commercial concepts (`ZIP code`,
  `Social Security number`, `ballpark figure`, `touch base`) in docs, diagnostics, and UI strings. Pick examples that
  make sense to someone outside the US.
- **Tooling stays out of published artifacts.** If we adopt a spell checker (e.g. `cspell` with an `en-GB` dictionary),
  it belongs in CI or contributor tooling only — never a dependency of `Core`, `UI`, or either stub.

## 5. C# conventions

Follow what the codebase already does; consistency beats personal preference.

- File-scoped namespaces. One namespace per folder that matches the folder name (`PolyInstall.Install`,
  `PolyInstall.Pal`, …).
- Nullable enabled everywhere. No `!` suppression, no `pragma warning disable` without an explanatory comment.
- `sealed` classes by default; `static` classes for stateless coordinators/engines (`InstallCoordinator`, `TaskEngine`).
- Collection expressions (`[]`, `[docs]`) over `new[] { … }` / `new List<T>()`; collection expressions for empty lists.
- `ArgumentNullException.ThrowIfNull(...)` at public entry points. Guard clauses at the top, early returns, no `else`
  after `throw`/`return`.
- Async only where genuinely async (file/process IO). Do not use `async void`. Always thread `CancellationToken`
  through long-running loops and check it between phases.
- Path safety: always `Path.Combine`/`Path.GetFullPath`; never string-concatenate paths; compare paths with
  `StringComparison.Ordinal` (`OrdinalIgnoreCase` where Windows semantics are required).
- No comments unless the *why* is non-obvious. No commented-out code, no TODO comments without an issue reference, no
  "changed by <agent>" markers. XML doc comments only on public API with genuinely useful semantics.
- Naming: intention-revealing, no abbreviations except established ones (`PAL`, `ARP`, `RID`, `PEG`-style none here).
  Suffix platform implementations consistently (`WindowsServiceManagerPal`, `LinuxFileAssociationPal`,
  `MacOsLaunchdServiceManagerPal`).
- Keep dependencies on framework APIs that trimming understands. Reflection over our own types in `Core`/`Runtime` risks
  being trimmed away: use source-generated JSON (`InstallJsonContext`) and avoid dynamic assembly loading. If you must
  reflect, add trim annotations and a test that exercises the trimmed `Runtime` publish path.

### Tests

- Every test follows the established conventions and best practice for C# and xUnit. Framework: xUnit + OmniAssert.
  Use the fluent `Must()` form; xUnit `Assert` only where OmniAssert has no equivalent.
- Naming: `<MethodUnderTest>_Should<Outcome>_When<Condition>`, in PascalCase so the name is a valid C# method
  (e.g. `ReadFooter_ShouldFindBundleFooter_WhenSignatureBytesFollowFooter`). No `Test` or `Test1` suffixes.
- Tests live in `src/tests/PolyInstall.Core.Tests` or `src/tests/PolyInstall.Core.Build.Tests`, mirroring the source
  folder structure. Core internals are visible to tests via `InternalsVisibleTo` — prefer testing through internal seams
  over making things public just to test them.
- **Every test must pass on Windows, Linux, and macOS.** CI runs all three. No OS-skipping to make red turn green: if a
  behaviour is platform-specific, test the PAL implementation in isolation with injected collaborators, never by calling
  real `sc.exe`, `launchctl`, `systemd`, or the Windows registry in CI.
- Provide test doubles for PAL interfaces (`NoOp…`/fake implementations) rather than mocks with reflection-heavy setups.
- Use temp directories with cleanup; never touch real install locations, user PATH, registry, or `/usr` in tests.
- New behaviour requires new tests; bug fixes require a regression test that fails before the fix.

---

## 6. Architecture invariants

**Build time / run time separation.** The CLI never rebuilds the stub; it appends `[stub][manifest JSON][compressed
zip][20-byte footer]`. Anything computed at build time must be embedded (e.g. `PayloadFeatureIndex`) — the runtime stub
does not glob, does not parse YAML, and does not validate against the JSON schema. Do not reintroduce those costs into
`Core`.

**Bundle format is a compatibility contract.** `InstallPayloadTrailer` (`FooterSize`, magic `POLYIN01`, little-endian
layout) and the backward footer scan that keeps signed installers working are frozen behaviour. Any change needs a
versioned magic/footer, a documented migration path, and trailer round-trip tests including the "data appended after the
footer" case.

**Manifest backward compatibility.** Manifests are user assets that outlive our releases.
- New manifest keys must be optional with the previous behaviour preserved when absent.
- Never rename or remove a documented key without a deprecation window and a clear diagnostic.
- Unknown/ignored fields must produce a diagnostic, not a crash.
- Removing a `features:` block means "everything is core" — never reinterpret silently.

**Install state is a contract too.** `<install-dir>/.polyinstall/{install-state.json,embedded-manifest.json}` drive
update, repair, and uninstall. Writes must be atomic (temp + replace), tolerant of partial corruption, and must remain
readable by older uninstallers. Never store absolute machine-specific paths where a placeholder (`{AppDir}`) will do.

**Feature gating is universal.** Anything conditionally installed (files, tasks, associations, services) flows through
`FeatureFilter` with `selectedFeatures`. New installable concepts must be gated the same way, and the feature→file index
`PayloadFeatureIndex` must stay in sync with what copy/cleanup logic actually honours.

**Task engine.** Actions are dispatched in `TaskEngine.Dispatch`. New actions: add one `case`, validate parameters once in
the semantic validator, throw `PlatformNotSupportedException` when the PAL capability is null, reject install-only
actions in uninstall phases explicitly, document it in the manifest reference, extend coverage in the samples only if it
is broadly useful.

**UI.** The wizard reads `InstallBootstrap`; it never performs install work itself. All file IO, elevation, and task
execution happen on background threads with progress callbacks. Do not block the UI thread; do not add business logic to
views or code-behind.

---

## 7. Weight budget (the "lightweight" rule)

Every change is evaluated for what it adds to the artifact the *end user* downloads.

- **Stub size is the headline metric.** `PolyInstall.Runtime` is published single-file + self-contained + trimmed. Before
  merging anything that touches `Core`, `Core.Build→Core` boundaries, `UI`, or either stub csproj, publish for one RID
  and report the size delta in the PR. Regressions need justification; avoid them by design.
- Keep the trimmer's promise: no unbounded reflection, no assembly scanning, no `dynamic`, no unnecessary NuGet packages.
  Prefer source-generated `System.Text.Json` over reflection-based serialization on hot/published paths.
- Prefer fewer, smaller dependencies; prefer BCL functionality over a package. A new package in `Core` or `UI` costs
  every installer; a new package in `Core.Build`/`Cli` only costs packagers.
- Do not add build-time output to published artifacts (strip PDBs, keep `.gitkeep`-style noise out of `stubs/`).
- Runtime memory and startup matter: stream instead of buffering whole payloads where feasible, reuse buffers, avoid
  loading the entire manifest/payload into memory more than once, and keep the installer responsive during long phases.
- The payload is the user's bytes: never require re-encoding, re-zipping, or transforming payload contents for
  convenience. `build.compression` choices must remain a transparent trade-off between size and speed, nothing more.

---

## 8. Security requirements (installers are privileged code)

Installers run elevated, write to system locations, mutate PATH/registry/services, and unpack untrusted-looking
archives. Assume adversarial input.

- **Extraction:** preserve and extend the zip-slip defence in `ZipPayloadExtractor` — every entry resolves under the
  destination; absolute paths, `..`, backslashes, and symlink parents are rejected. Never weaken it for convenience.
- **Command execution:** any process launch (`CommandRunner`, PAL implementations) must use `ArgumentList`, never string
  concatenation into a shell. No `cmd -c` / `sh -c` with user-controlled content. Quote or avoid shell metacharacters in
  generated desktop entries, `.lnk` targets, plists, and systemd unit content.
- **Least privilege:** only request elevation when actually required (machine-wide scope, system services). Prefer user
  scope by default. Never silently escalate.
- **Temp usage:** extraction directories must be created with restrictive ACLs/permissions (`700`/user-only), cleaned up
  on success and failure, and must not be predictable, world-writable paths. Do not execute anything from a
  world-writable location.
- **Secrets:** signing certificates, passwords, keychain paths, and notary credentials arrive via environment
  substitution (`${VAR}`) only. Never log them, never persist them into `.polyinstall/`, logs, `BuildOutputManifest`, or
  the `--json` output stream. Redact before printing any command line.
- **Uninstall safety:** uninstall must never delete files outside the install root, must refuse to delete the install
  root if unexpected non-payload files remain unless explicitly allowed, and must always stop/remove registered services
  before deleting binaries.
- **Input validation:** manifest-driven paths from YAML are untrusted input. Validate before use (no traversal out of
  scope, no absolute paths outside declared roots, no control characters in service/shortcut names).

---

## 9. Documentation, schema, and example hygiene

- Manifest model changed → regenerate `schema/v1.json` in the **same commit**. CI/users rely on the published schema URL.
- User-visible behaviour changed → update the docs site content, `examples/polyinstall.sample.yaml` (keep it a minimal,
  copy-pasteable starting point for **non-.NET** payloads), README claims, and `ARCHITECTURE.md` when structure moved.
- Diagnostics are user interface. `ManifestSemanticValidator` diagnostics must state: what is wrong, where (source line
  when available via `YamlSourceIndex`), and how to fix it. Prefer collecting multiple diagnostics over failing on the
  first error.
- Never leave docs describing behaviour the code no longer has; never ship behaviour with no documentation.

---

## 10. Workflow expectations for agents

**Before writing code:** read the relevant files named above, state the plan, and confirm which constraints in §1–§7 it
affects. Small, reviewable diffs; one concern per commit/PR.

**Definition of done:**

```bash
dotnet build src/PolyInstall.slnx -c Release          # zero new warnings
dotnet test  src/PolyInstall.slnx -c Release          # green (must pass on win/linux/mac)
dotnet run --project src/PolyInstall.SchemaGen/…      # if manifest models changed (commit schema/v1.json)
<publish stubs + run the sample build>                # if Core/UI/stub/pipeline behaviour changed
```

**Every PR must answer:** What is the stub/artifact size impact? Which platforms were considered, and what is the
behaviour where unsupported? Are new manifest keys optional and backward compatible? Which tests cover it?

**Do not:**
- Add a package to `PolyInstall.Core` or `PolyInstall.UI` without explicit justification and sign-off in the PR.
- Add platform checks outside `Core/Pal/`.
- Assume the payload is .NET, or require `dotnet` at install time.
- Weaken the trailer format, zip-slip guard, or elevation rules.
- Skip tests because they are hard to run cross-platform — build a PAL fake instead.
- Commit build artifacts (`bin/`, `obj/`, `stubs/`, `dist/`), credentials, or machine-specific absolute paths.
- Rewrite large areas opportunistically. Refactors are separate from behaviour changes unless the refactor is required
  to make the change correct.

**Branch names** use a type prefix: `ft/…`, `ch/…`, `doc/…`, `tst/…`, `fix/…`.

**Commit messages** have a subject line, one blank line, then an itemised list of every change in the commit.

1. Read `$GIT_BRANCH_NAME`. If it contains a GitHub issue id (`#123`, or a numeric issue segment such as `ft/123-…`) or a
   Jira ticket id (an uppercase project key, a hyphen, and digits, such as `PI-14`), use that id alone as the subject.
   When several ids are present, use the first one.
2. If no issue or ticket id can be extracted, use the change type, a colon, and a brief single-line summary of the whole
   changeset. Types match the branch prefixes: `ft`, `ch`, `tst`, `doc`, `fix` (for example
   `ft: add localappdata install target`).
3. Leave one blank line, then list each change on its own line.

```text
PI-14

- accept {LocalAppData} as an install target
- document the placeholder in the sample manifest
```

```text
ft: add localappdata install target

- accept {LocalAppData} as an install target
- document the placeholder in the sample manifest
```

Releases are triggered only by bumping `<Version>` in `src/Directory.Build.props`; never hand-edit release assets.
