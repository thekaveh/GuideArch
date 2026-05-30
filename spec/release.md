# Release artifacts (M5) — formal specification

**Status:** Authoritative.

M5 ships the v1.0.0 release: build configs + a GitHub Actions release workflow that, on a `v*.*.*` tag push, builds artifacts for every supported impl/platform and attaches them to the GitHub Release.

## 1. Targets

### 1.1 TypeScript + Tauri 2

- **Desktop installers** for Win (`.msi`), macOS (`.dmg`), Linux (`.AppImage`, `.deb`) via `pnpm tauri build`. Built on the corresponding GitHub Actions runner per platform.
- **Web bundle** — `pnpm build` output (`langs/typescript/build/`), zipped and attached as `guidearch-web-v1.0.0.zip`.

Signing / notarization are stretch goals; v1.0.0 ships unsigned with an "unsigned binary" note in release notes (users on macOS will need to right-click → Open).

### 1.2 C# + Avalonia

- **Desktop binaries** — `dotnet publish -c Release -r <rid> --self-contained -p:PublishSingleFile=true` for `win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`. Each emits a single-file executable, zipped and attached.
- **WebAssembly** — `dotnet publish -c Release -r browser-wasm` (Avalonia.Browser) emits a static bundle, zipped and attached. (If Avalonia.Browser isn't set up at M5, defer to v1.1 and ship desktop only.)

### 1.3 Python + NiceGUI

- **PyPI package** — `uv build` produces `dist/guidearch-1.0.0-py3-none-any.whl` and `guidearch-1.0.0.tar.gz`. Attach to GitHub Release. Optional: publish to PyPI via `twine` if `PYPI_API_TOKEN` secret is configured; otherwise skip.
- **Docker image** — `docker buildx build --platform linux/amd64,linux/arm64` on a `Dockerfile` at `langs/python/Dockerfile` that copies the package, installs deps via `uv sync`, exposes port 8080, and runs `uv run guidearch`. Push to `ghcr.io/thekaveh/guidearch:1.0.0` if `GHCR_TOKEN` is configured; otherwise skip push but build to validate.

## 2. CI workflow

`.github/workflows/release.yml`:

- Triggers on `push` of tag matching `v*.*.*` (and `workflow_dispatch` for testing).
- Five jobs in parallel:
  - `tauri-build` — matrix over Ubuntu/macOS/Windows; builds installers; uploads as workflow artifacts.
  - `avalonia-desktop` — matrix over the 4 RIDs; publishes self-contained; uploads.
  - `python-package` — builds wheel + sdist; uploads.
  - `python-docker` — builds + (optionally) pushes Docker image.
  - `web-bundle` — `pnpm build` + zip; uploads.
- One final job `release` that depends on all five: downloads all artifacts, creates a GitHub Release with auto-generated notes from commits since the previous tag, attaches every artifact.

## 3. Artifacts list (per v1.0.0 tag push)

| Artifact | Source |
|---|---|
| `guidearch-1.0.0-x86_64.AppImage` | Tauri (Ubuntu) |
| `guidearch_1.0.0_amd64.deb` | Tauri (Ubuntu) |
| `GuideArch_1.0.0_x64-setup.exe` | Tauri (Windows) |
| `GuideArch_1.0.0_x64_en-US.msi` | Tauri (Windows) |
| `GuideArch_1.0.0_universal.dmg` | Tauri (macOS) |
| `guidearch-web-1.0.0.zip` | SvelteKit static build |
| `GuideArch-Avalonia-1.0.0-win-x64.zip` | dotnet publish |
| `GuideArch-Avalonia-1.0.0-osx-x64.zip` | dotnet publish |
| `GuideArch-Avalonia-1.0.0-osx-arm64.zip` | dotnet publish |
| `GuideArch-Avalonia-1.0.0-linux-x64.zip` | dotnet publish |
| `guidearch-1.0.0-py3-none-any.whl` | uv build |
| `guidearch-1.0.0.tar.gz` | uv build |
| `ghcr.io/thekaveh/guidearch:1.0.0` | Docker image (registry, not GitHub Release attachment) |

## 4. Versioning

- `package.json` (TS): `"version": "1.0.0"`
- `*.csproj` (C#): set `<Version>1.0.0</Version>` in `Directory.Build.props`
- `pyproject.toml` (Python): `version = "1.0.0"`
- `Cargo.toml` (Tauri shell): `version = "1.0.0"`
- `tauri.conf.json`: same

All five must agree at release time.

## 5. Release notes

The workflow auto-generates notes from `git log v0.4.0-m4..v1.0.0` grouped by Conventional Commits prefix. The release body includes:

- Summary one-liner
- Highlights (top 3-5 commits by impact)
- "What's in v1.0.0" — pointing at the README §1 description of feature scope
- Asset table (mirrors §3 above)
- Known issues / unsigned binary note

## 6. Out of scope for v1.0.0

- Auto-update channels (Tauri's updater)
- Code signing on Windows / macOS notarization
- ARM-Linux desktop binaries
- Snap / Flatpak packages
- PyPI publish if no token configured (artifact is built but not pushed)
