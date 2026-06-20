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
  <a href="https://github.com/bolorundurowb/PolyInstall/releases">
    <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/bolorundurowb/PolyInstall/total">
  </a>
</p>

**PolyInstall** is a manifest-driven installer generator. Package your applications into cross-platform,
self-extracting binaries from a single YAML file, with a modern Avalonia-based installation UI for Windows, Linux,
and macOS.

> This project was developed with the help of **generative AI** tools.
> Treat the code, manifests, and documentation accordingly: verify behaviour, review changes before you rely on them in
> production, and apply your own judgement and testing.

## Documentation

For detailed guides, manifest reference, examples, CLI options, and troubleshooting, see the
**[PolyInstall documentation](https://bolorundurowb.github.io/PolyInstall/)**.

## Key features

- **YAML manifests** — metadata, files, build targets, UI, services, and tasks in one file
- **Cross-platform installers** — Windows (.exe), Linux (AppImage), and macOS (DMG)
- **Service registration** — Windows services, Linux systemd units, and macOS launchd jobs
- **Avalonia UI** — responsive installer wizard across desktop platforms

## Quick start

1. Download the `polyinstall` zip for your host OS from
   **[GitHub Releases](https://github.com/bolorundurowb/PolyInstall/releases)** and extract it (`polyinstall` and
   `stubs/` should sit in the same folder).

2. Author a manifest — start from [`examples/polyinstall.sample.yaml`](examples/polyinstall.sample.yaml) and set
   `metadata`, `files`, and `build.targets`.

3. Build your installer:

   ```bash
   ./polyinstall build /path/to/your.manifest.yaml --base /path/to/payload-root
   ```

   On Windows, run `polyinstall.exe` instead of `./polyinstall`.

4. Run the produced installer on a machine that matches the target you built.

Built files land under `build.output_dir` from the manifest (relative to `--base`). For CI integration, signing,
manifest schema, and building from source, see the [documentation](https://bolorundurowb.github.io/PolyInstall/).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and pull request guidance.

## License

See [LICENSE](LICENSE). Third-party notices are in [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).
