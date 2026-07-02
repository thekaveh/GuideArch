# Consumed Contract Ledger

**Status:** Living maintenance artifact. Update this file whenever a pinned
external dependency, invoked CLI, Docker image, GitHub Action, or vendored
submodule contract changes.

## 1. Purpose

The maintenance loop verifies GuideArch against the exact contracts it consumes,
not against memory or mocks. Lockfiles remain the source of dependency
resolution; this ledger records the human-readable contract checks that should
be reviewed when those pins move.

## 2. VMx Framework

| Consumer | Pinned contract | Latest-status check | Public surface verified | Method |
|---|---|---|---|---|
| Python runtime | PyPI `vmx==3.1.0` in `langs/python/pyproject.toml` / `uv.lock` | PyPI package lookup during 2026-07-02 maintenance | `RelayCommandOf`, VM builder/component APIs, property-change integration | Python unit/integration suite, VMx compatibility tests, conformance runner |
| TypeScript runtime | `vendor/vmx` submodule `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12`, VMx package `3.1.0` | `git describe` reports `python-v1.0.0-208-ga77b25a` at current pin | VMx imports used by viewmodels and `vmx-to-svelte` adapter | VMx TS dist build, Svelte type-check, Vitest suite, conformance runner |
| C# runtime | `vendor/vmx` submodule `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12`, VMx project version `3.1.0` | Same submodule pin as TypeScript | `RelayCommand`, `ComponentVM`, service interfaces used by factories | `dotnet build`, xUnit VM/model suite, conformance runner |

## 3. Python Runtime And Packaging

| Integration point | Pinned contract | Latest-status check | Public surface verified | Method |
|---|---|---|---|---|
| NiceGUI | `nicegui==3.13.0` in `uv.lock`; project allows `nicegui>=3.13.0` | PyPI lookup found newer `3.14.0` during 2026-07-02 audit | `ui.run`, `@ui.page`, Quasar component props used by `main.py` | Python suite plus manual source trace; upgrade-policy follow-up remains open |
| pywebview | `pywebview==6.0.3` in `uv.lock`; project allows `pywebview>=5.0` | Lockfile exact version checked | Native-mode launch handoff contract | CLI/help trace and source review; native window launch not run in maintenance loop |
| Hatchling/uv build | `uv.lock` plus pinned Docker `uv:0.8` image digest | Dockerfile digest checked | `uv sync --no-dev`, `uv build` | `uv build`, Docker buildx check |

## 4. TypeScript, Svelte, And Tauri

| Integration point | Pinned contract | Latest-status check | Public surface verified | Method |
|---|---|---|---|---|
| Vite | `vite@6.4.3` in `pnpm-lock.yaml` | `pnpm audit --audit-level low` clean after lock refresh | Vite dev/build CLI used by scripts | `pnpm build`, `pnpm check`, audit |
| esbuild | Vite chain: `esbuild@0.25.12`; tsx chain: `esbuild@0.28.1` in `pnpm-lock.yaml` | `pnpm audit --audit-level low` clean after lock refresh | Vite/tsx transitive binary installs | frozen `pnpm install`, audit |
| SvelteKit/Svelte | `@sveltejs/kit@2.61.1`, `svelte@5.56.3` in `pnpm-lock.yaml` | `pnpm outdated` reviewed during audit | Svelte 5 component/compiler contracts | `pnpm check`, `pnpm build`, Vitest component/source tests |
| Tauri CLI/runtime config | `@tauri-apps/cli@2.11.2`, Cargo lock under `src-tauri/` | Package lock reviewed | `tauri.conf.json`, capability config, Rust entry point | Source/config trace; full `pnpm tauri build` deferred because it is heavyweight |

## 5. C# And Avalonia

| Integration point | Pinned contract | Latest-status check | Public surface verified | Method |
|---|---|---|---|---|
| .NET SDK/runtime | CI uses `actions/setup-dotnet@v5` with `8.0.x`; local RollForward permits newer runtime | CI workflow review | `dotnet restore/build/test/format/run` commands | Local `dotnet build`, `dotnet test`, `dotnet format`, conformance runner |
| Avalonia | Direct NuGet versions in C# project files; no lockfile yet | `dotnet list package --vulnerable --include-transitive` clean | XAML controls, storage provider, app bootstrap | xUnit markup tests, source trace; NuGet lockfile follow-up remains open |
| ScottPlot/Avalonia charts | Direct NuGet package versions in view project | Project-file review | Chart rendering and theming APIs | xUnit chart/theme tests; visual snapshot automation follow-up remains open |

## 6. CI, Release, And Build Infrastructure

| Integration point | Pinned contract | Latest-status check | Public surface verified | Method |
|---|---|---|---|---|
| GitHub Actions | Workflow action refs such as `actions/checkout@v6`, `setup-python@v6`, `setup-node@v6`, `setup-dotnet@v5`, `docker/build-push-action@v6` | Workflow source review | Inputs used in CI/release jobs | Static workflow trace; actionlint follow-up remains open |
| Docker base image | `python:3.14-slim@sha256:d7a925f9eb9639a93e455b9f12c167569358818c0f62b51b88edbc8fcf34c421` | Digest pinned in Dockerfile | Python runtime image contract | `docker buildx build --check -f langs/python/Dockerfile langs/python` |
| Docker uv image | `ghcr.io/astral-sh/uv:0.8@sha256:1d31be550ff927957472b2a491dc3de1ea9b5c2d319a9cea5b6a48021e2990a6` | Digest pinned in Dockerfile | `/uv` binary copy path | Docker buildx check |
| pnpm | CI pins pnpm action `version: '11'`; local run used pnpm `11.5.0` | `pnpm --version`/config observed during audit | install, audit, build, test scripts | frozen install, audit, TypeScript suite |

## 7. Open Contract Follow-Ups

- Add CI audit gates for `pnpm audit`, `pip-audit`, NuGet vulnerability scan,
  Cargo audit/deny, actionlint, ShellCheck, and secret scanning.
- Add NuGet lock files or an equivalent locked restore policy for C#.
- Decide whether Python runtime dependencies should be hard-pinned/capped or
  tested in an explicit "latest allowed direct dependencies" compatibility lane.
- Add a restrictive production CSP for the Tauri webview.
