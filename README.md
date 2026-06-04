# GuideArch

Modern fuzzy multi-criteria decision analysis for software architecture, shipped as three concurrent implementations sharing one spec.

GuideArch helps software architects pick between competing technology stacks by modeling a decision space — *decisions*, *alternatives*, quality *properties* with priority weights, and three families of *constraints* — and ranking the resulting candidate architectures using fuzzy TOPSIS. It also reports which decisions matter most (sensitivity analysis) and which constraints are most binding (elimination counting).

## 1. Status

**v1.0.0 released — M5 complete — release artifacts wired.** All three implementations (TypeScript, C#, Python) are feature-complete: full domain + TOPSIS engine, editors, results, analysis charts, and release-ready build configs. Tauri installers, Avalonia self-contained binaries, Python wheel + Docker image, and a SvelteKit static web bundle are all produced by the GitHub Actions release workflow on every `v*.*.*` tag push.

Milestone tags: `v0.0.0-bootstrap` (M0), `v0.1.0-m1` (M1 — domain + TOPSIS), `v0.2.0-m2` (M2 — ViewModels + skeleton UI), `v0.3.0-m3` (M3 — full editors), `v0.4.0-m4` (M4 — analysis + charts), `v1.0.0` (M5 — release artifacts).

## 2. What's in the box

Three implementations of the same application, kept in conformance by a shared spec and a cross-impl test corpus.

| # | Language | UI framework | Desktop | Web |
|---|---|---|---|---|
| 2.1 | TypeScript | Svelte 5 + [Tauri 2](https://tauri.app) | ✓ | ✓ |
| 2.2 | C# | [Avalonia 12](https://avaloniaui.net) | ✓ | deferred to v1.1 (see `spec/release.md` §1.2) |
| 2.3 | Python | [NiceGUI 3.x](https://nicegui.io) | ✓ (pywebview) | ✓ |

All three are built on the [VMx](https://github.com/thekaveh/VMx) MVVM framework, included as a git submodule at `vendor/vmx/`.

## 3. Documentation hub

Read most-to-least essential. Each link includes when to use it.

### 3.1 Specification & conformance

- **[`spec/`](spec/README.md)** — the language-neutral source of truth that every implementation must satisfy. Contents:
  - **[`spec/algorithms/topsis.md`](spec/algorithms/topsis.md)** — the canonical TOPSIS pipeline with magic-number table and tie-break rule. *(M1)*
  - **[`spec/algorithms/critical-decisions.md`](spec/algorithms/critical-decisions.md)**, **[`spec/algorithms/critical-constraints.md`](spec/algorithms/critical-constraints.md)** — reference cards. *(M1)*
  - **[`spec/domain/scenario.schema.json`](spec/domain/scenario.schema.json)** — JSON Schema 2020-12 for the input format. *(M1)*
  - **[`spec/domain/glossary.md`](spec/domain/glossary.md)**, **[`spec/domain/invariants.md`](spec/domain/invariants.md)** — vocabulary and load-time validation rules. *(M1)*
  - **[`spec/viewmodels.md`](spec/viewmodels.md)** — shared ViewModel tree shape: command names, observable property names, dirty-tracking, and re-solve trigger lists every impl mirrors. *(M2)*
  - **[`spec/editors.md`](spec/editors.md)** — editor semantics: cascade rules for Delete (decision → alternatives → coefficients → constraints), add-with-defaults behavior, validation timing. *(M3)*
  - **[`spec/charts.md`](spec/charts.md)** — chart contracts: fuzzy-decomposition triangle layout, axis/series conventions, color tokens. *(M4)*
  - **[`spec/design-system.md`](spec/design-system.md)** — the visual language (color tokens, type scale, spacing, component specs) that all three impls render against. *(v1.0)*
  - **[`spec/release.md`](spec/release.md)** — release process, versioning policy, monorepo tag scheme. *(v1.0)*
  - **[`spec/conformance/`](spec/conformance/)** — the seed corpus: `scenarios/sas.json` (10 decisions, 25 alternatives, 7 properties), `scenarios/eds.json` (same shape), and the matching `expected/*.json` outputs the three impls must reproduce within `tolerances.json` (1e-9 absolute on scalars; ranking exact). *(M1, expanded M2-M4)*

### 3.2 Architecture decision records

Numbered rationale for each non-obvious design choice. Read when questioning *why* something is the way it is.

- [ADR-0001 — Three implementations sharing one spec; VMx as submodule](spec/ADRs/0001-three-impls-vmx-submodule.md)
- [ADR-0002 — JSON Schema for scenario files (not legacy XML)](spec/ADRs/0002-json-schema-not-xml.md)
- [ADR-0003 — TOPSIS as in-repo code; no Microsoft Solver Foundation](spec/ADRs/0003-topsis-no-msf.md)
- [ADR-0004 — MIT License](spec/ADRs/0004-mit-license.md)
- [ADR-0005 — Single monorepo version; all three impls release together](spec/ADRs/0005-single-monorepo-version.md)
- [ADR-0006 — NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)](spec/ADRs/0006-nicegui-over-shiny.md)

### 3.3 Contributing & governance

- **[CONTRIBUTING.md](CONTRIBUTING.md)** — feature workflow (spec-first, all three impls in lockstep), local development, test layout, code style per language. *Read before opening a PR.*
- **[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)** — Contributor Covenant 2.1. Report violations to kaveh.razavi@gmail.com.
- **[SECURITY.md](SECURITY.md)** — private vulnerability reporting.

## 4. Repository layout

```
spec/                language-neutral spec (schema, algorithms, conformance corpus, ADRs)
vendor/vmx/          VMx submodule (do not edit directly; PR upstream)
langs/typescript/    TS + Tauri 2 + Svelte 5 implementation
langs/csharp/        C# + Avalonia 12 implementation
langs/python/        Python + NiceGUI 3.x implementation
tools/               cross-cutting scripts (VMx mode switch, legacy XML import)
.github/             CI workflows, issue/PR templates, dependabot config
```

## 5. Quickstart

### 5.0 Clone with submodules

VMx lives at `vendor/vmx/`. Without it initialized, every impl below fails because VMx imports won't resolve.

```bash
git clone --recurse-submodules https://github.com/thekaveh/GuideArch.git
# or, if already cloned without --recurse-submodules:
cd GuideArch
git submodule update --init
```

### 5.1 Prerequisites

| Tool | Used by | Check | Install |
|---|---|---|---|
| Node 22+ | TypeScript | `node --version` | `brew install node@22` |
| pnpm 11+ | TypeScript | `pnpm --version` | `npm install -g pnpm@latest` |
| Rust + cargo | TypeScript (Tauri desktop only) | `cargo --version` | https://rustup.rs |
| .NET 8 or 9 SDK | C# | `dotnet --list-sdks` | `brew install --cask dotnet-sdk` |
| Python 3.11+ | Python | `python3 --version` | `brew install python@3.12` |
| uv | Python | `uv --version` | `curl -LsSf https://astral.sh/uv/install.sh \| sh` |

### 5.2 TypeScript (Tauri 2 + Svelte 5)

```bash
cd langs/typescript
pnpm install
pnpm dev          # web mode — browser at http://localhost:1420
pnpm tauri dev    # desktop mode — native Tauri window (first build is slow)
```

`Ctrl-C` (or close the window) to stop.

### 5.3 C# (Avalonia 12)

```bash
cd langs/csharp
dotnet build
dotnet run --project src/GuideArch.View   # desktop Avalonia window
```

Close the window to stop. The Avalonia WebAssembly target is deferred to v1.1 (see `spec/release.md` §1.2); v1.0 C# ships desktop-only.

### 5.4 Python (NiceGUI 3.x)

```bash
cd langs/python
uv sync
uv run guidearch                       # web mode — browser at http://localhost:8080
uv run guidearch --native              # desktop mode — native pywebview window
uv run python -m guidearch.main --native   # equivalent module-form invocation
```

`Ctrl-C` (or close the window) to stop.

The two `--native` forms behave identically. The console-script form
(`uv run guidearch --native`) detects the flag and re-execs itself as
`python -m guidearch.main` before booting NiceGUI, so the multiprocessing
spawn child that drives pywebview has a stable package-qualified `__main__`
to import — without that handoff, on some platforms the parent's HTTP
server starts (you see `NiceGUI ready to go on http://127.0.0.1:8080`) but
the pywebview window never surfaces.

What native mode actually is: a borderless desktop window backed by
pywebview's WebKit (macOS) / WebView2 (Windows) / GTK-WebKit (Linux). The
UI inside is the same Svelte-style NiceGUI view you'd see in the browser —
only the chrome differs. The `127.0.0.1:8080` line is still printed in
native mode because pywebview talks to NiceGUI's local HTTP server; you
don't need to open it manually.

### 5.5 Try the sample scenarios

Each app ships **SAS** (Service-Oriented Architecture, 10 decisions / 25 alternatives / 7 properties) and **EDS** (Enterprise Decision Space, similar shape) as bundled samples. After launching any flavor, click the toolbar button **Open Sample SAS** (or **Open Sample EDS**) — the candidates table populates immediately. The JSON form of each scenario lives under [`spec/conformance/scenarios/`](spec/conformance/scenarios/); the original legacy XML files are not committed to this repository, but [`tools/import-legacy-xml.py`](tools/import-legacy-xml.py) is the converter that produced the bundled JSON from them.

The recommended exploration flow:

1. **Open Sample SAS** in the toolbar.
2. **Results tab** — top candidate's score should be `0.031180695179944085`. The bar chart on the right shows the top 30; click any bar to jump to that candidate.
3. **Properties tab** — change one property's weight (e.g., bump *Reliability* to 9). Watch the candidates table refresh instantly (v1.0 re-solves synchronously — see the v1.0 status note at the top of `spec/editors.md`; at SAS/EDS scale a single solve is under 10 ms).
4. **Critical decisions tab** — see which architectural choices drive the result most.
5. **Critical constraints tab** — see which constraints eliminate the most candidates.
6. **Save As…** to a temp file; **New**; then **Open…** the file you just saved — the edit should round-trip.

### 5.6 Run the conformance suite

Each impl ships a runner that solves every scenario in `spec/conformance/scenarios/` and compares against `spec/conformance/expected/` within `1e-9` absolute. CI fails on divergence.

```bash
# Python
cd langs/python && uv run python -m guidearch.conformance.runner

# C#
cd langs/csharp && dotnet run --project src/GuideArch.Conformance

# TypeScript
cd langs/typescript && pnpm conformance
```

### 5.7 Run the unit + integration test suites

Each impl ships VM-layer integration tests (load scenario → exercise ViewModel → assert results — no UI mounted) that prove MVVM separation works.

```bash
cd langs/python     && uv sync --all-extras && uv run pytest tests/ -q
cd langs/csharp     && dotnet test --nologo
cd langs/typescript && pnpm test
```

The Python `--all-extras` flag installs the `dev` group (`pytest`, `mypy`, `ruff`); the bare `uv sync` used in §5.4 is the runtime-only path.

### 5.8 Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| TS web shows blank page | Stale build with module-init filesystem access | Pull latest; re-run `pnpm dev` |
| `pnpm tauri dev` fails with cargo errors | Rust toolchain too old or missing Linux deps | Re-run rustup; on Ubuntu install `libwebkit2gtk-4.1-dev libsoup-3.0-dev` |
| `dotnet run` says "must install .NET 8" | No .NET 8 runtime installed | Install .NET 8 runtime or rely on `RollForward=Major` (already set in `Directory.Build.props`) |
| `uv run guidearch` fails on import | submodule not initialised | `git submodule update --init` |
| OS file picker never appears | macOS file permissions for the terminal app | System Settings → Privacy → Files & Folders |

## 6. License

MIT — see [LICENSE](LICENSE).
