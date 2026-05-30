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
| 2.2 | C# | [Avalonia 12](https://avaloniaui.net) | ✓ | ✓ (WebAssembly) |
| 2.3 | Python | [NiceGUI 3.x](https://nicegui.io) | ✓ (pywebview) | ✓ |

All three are built on the [VMx](https://github.com/thekaveh/VMx) MVVM framework, included as a git submodule at `vendor/vmx/`.

## 3. Documentation hub

Read most-to-least essential. Each link includes when to use it.

### 3.1 Architecture & design

- **[Design specification](docs/design/2026-05-29-guidearch-rewrite-design.md)** — the full architectural spec: domain model, TOPSIS algorithm, per-impl design, conformance strategy, GitHub plan, v1 scope, M0–M5 roadmap. *Read this first if you want to understand the project, contribute to design, or evaluate the architecture.*

### 3.2 Milestone plans

- **[M0 — Repo bootstrap](docs/plans/2026-05-30-m0-repo-bootstrap.md)** — the 27-task implementation plan that produced the bootstrap state. *Read this if you want to understand how the scaffolding was assembled.*
- *M2–M5 plans land in [`docs/plans/`](docs/plans/) as each milestone begins. M1 was executed directly from the spec without a separate plan document because the algorithm was already fully specified in `spec/algorithms/topsis.md`.*

### 3.3 Specification & conformance

- **[`spec/`](spec/README.md)** — the language-neutral source of truth that every implementation must satisfy. Populated at M1 with:
  - **[`spec/algorithms/topsis.md`](spec/algorithms/topsis.md)** — the canonical TOPSIS pipeline with magic-number table and tie-break rule.
  - **[`spec/algorithms/critical-decisions.md`](spec/algorithms/critical-decisions.md)**, **[`critical-constraints.md`](spec/algorithms/critical-constraints.md)** — reference cards.
  - **[`spec/domain/scenario.schema.json`](spec/domain/scenario.schema.json)** — JSON Schema 2020-12 for the input format.
  - **[`spec/domain/glossary.md`](spec/domain/glossary.md)**, **[`invariants.md`](spec/domain/invariants.md)** — vocabulary and load-time validation rules.
  - **[`spec/conformance/`](spec/conformance/)** — the seed corpus: `scenarios/sas.json` (10 decisions, 25 alternatives, 7 properties), `scenarios/eds.json` (same shape), and the matching `expected/*.json` outputs the three impls must reproduce within `tolerances.json` (1e-9 absolute on scalars; ranking exact).

### 3.4 Architecture decision records

Numbered rationale for each non-obvious design choice. Read when questioning *why* something is the way it is.

- [ADR-0001 — Three implementations sharing one spec; VMx as submodule](spec/ADRs/0001-three-impls-vmx-submodule.md)
- [ADR-0002 — JSON Schema for scenario files (not legacy XML)](spec/ADRs/0002-json-schema-not-xml.md)
- [ADR-0003 — TOPSIS as in-repo code; no Microsoft Solver Foundation](spec/ADRs/0003-topsis-no-msf.md)
- [ADR-0004 — MIT License](spec/ADRs/0004-mit-license.md)
- [ADR-0005 — Single monorepo version; all three impls release together](spec/ADRs/0005-single-monorepo-version.md)
- [ADR-0006 — NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)](spec/ADRs/0006-nicegui-over-shiny.md)

### 3.5 Contributing & governance

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
docs/                design specs and milestone plans
.github/             CI workflows, issue/PR templates, dependabot config
```

## 5. Quickstart

At M1 each impl exposes the TOPSIS engine via a conformance runner plus a placeholder app (the M0 hello page, updated to "M1: domain ready"). The full app UI arrives in M2+.

**Clone with submodules** (VMx lives at `vendor/vmx/`):

```bash
git clone --recurse-submodules https://github.com/thekaveh/GuideArch.git
# or, if already cloned without --recurse-submodules:
git submodule update --init
```

Without the submodule initialized, every per-impl quickstart below fails (VMx imports won't resolve).

### 5.1 TypeScript (Tauri 2 + Svelte 5)

Requires Node 22+, pnpm, and Rust (for Tauri's native shell).

```bash
cd langs/typescript
pnpm install
pnpm dev          # web (browser at http://localhost:1420)
pnpm tauri dev    # desktop (native window)
```

### 5.2 C# (Avalonia 12)

Requires .NET 8 SDK or newer (the build targets `net8.0`).

```bash
cd langs/csharp
dotnet build
dotnet run --project src/GuideArch.View   # desktop window
```

### 5.3 Python (NiceGUI 3.x)

Requires Python 3.11+ and [uv](https://github.com/astral-sh/uv).

```bash
cd langs/python
uv sync
uv run guidearch           # web (browser at http://localhost:8080)
uv run guidearch --native  # desktop (pywebview window)
```

### 5.4 Run the conformance suite

Each impl ships a conformance runner that solves every scenario in `spec/conformance/scenarios/` and compares against `spec/conformance/expected/` within `1e-9` absolute. CI fails on divergence.

```bash
# Python
cd langs/python && uv run python -m guidearch.conformance.runner

# C#
cd langs/csharp && dotnet run --project src/GuideArch.Conformance

# TypeScript
cd langs/typescript && pnpm conformance
```

## 6. License

MIT — see [LICENSE](LICENSE).
