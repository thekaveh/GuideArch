# M0 — Repo Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a professional public GitHub repository at `thekaveh/GuideArch` with VMx wired as a submodule, three language scaffolds (TS+Tauri+Svelte, C#+Avalonia, Python+NiceGUI), CI green on all impls, and full governance scaffolding — ready for M1 to begin.

**Architecture:** Monorepo with `langs/{typescript,csharp,python}/` containing per-impl scaffolds, `vendor/vmx/` git submodule pinned to VMx upstream `main` HEAD at bootstrap (commit `e2b23f8`); see Task 11's fallback path for the rationale (`v2.1.0` tag does not yet exist upstream), `spec/` for language-neutral source-of-truth (skeleton only at M0), `.github/workflows/` for per-impl + cross-cutting CI. Each impl's scaffold ships a "hello world" that imports VMx as proof of wiring.

**Tech Stack:** Git submodule (VMx); Node 20 + pnpm + Vite + Svelte 5 + Tauri 2 (TS); .NET 8 SDK + Avalonia 12 (C#); Python 3.12 + uv + NiceGUI 3.x + pywebview (Python); GitHub Actions for CI.

---

## Hard rules (apply to every task)

1. **No AI/Claude mentions.** Every commit message, PR body, and file content uses plain professional language. **NEVER** append `Co-Authored-By: Claude …`, "Generated with …", or any equivalent. The default Claude Code commit footer must be **omitted**.
2. **Use Conventional Commits.** `feat:`, `fix:`, `docs:`, `chore:`, `ci:`, `build:`, `test:`.
3. **Stage files explicitly.** Use `git add <specific paths>`, never `git add .` or `-A`.
4. **Working directory** for every command unless otherwise stated: `/Users/kaveh/repos/GuideArch/`.

---

## File structure created by M0

```
GuideArch/
├── .gitignore
├── .gitattributes
├── .gitmodules
├── LICENSE
├── README.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── SECURITY.md
├── .github/
│   ├── workflows/
│   │   ├── spec.yml
│   │   ├── conformance.yml
│   │   ├── typescript.yml
│   │   ├── csharp.yml
│   │   ├── python.yml
│   │   └── vmx-bump.yml
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.md
│   │   ├── feature.md
│   │   ├── conformance-divergence.md
│   │   └── question.md
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── dependabot.yml
├── spec/
│   ├── README.md
│   ├── domain/.keep
│   ├── algorithms/.keep
│   ├── conformance/.keep
│   └── ADRs/
│       ├── 0001-three-impls-vmx-submodule.md
│       ├── 0002-json-schema-not-xml.md
│       ├── 0003-topsis-no-msf.md
│       ├── 0004-mit-license.md
│       ├── 0005-single-monorepo-version.md
│       └── 0006-nicegui-over-shiny.md
├── vendor/
│   └── vmx/                              # submodule
├── tools/
│   ├── use-vmx-local.sh
│   ├── use-vmx-released.sh
│   └── import-legacy-xml.py              # stub for M1
├── langs/
│   ├── typescript/
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── vite.config.ts
│   │   ├── svelte.config.js
│   │   ├── .eslintrc.cjs
│   │   ├── .prettierrc
│   │   ├── index.html
│   │   ├── src/
│   │   │   ├── main.ts
│   │   │   ├── App.svelte
│   │   │   └── lib/hello-vmx.ts
│   │   └── src-tauri/                    # Tauri 2 shell
│   ├── csharp/
│   │   ├── GuideArch.sln
│   │   ├── Directory.Build.props
│   │   ├── src/
│   │   │   ├── GuideArch.Models/
│   │   │   ├── GuideArch.ViewModels/
│   │   │   └── GuideArch.View/
│   │   └── tests/.keep
│   └── python/
│       ├── pyproject.toml
│       ├── src/guidearch/
│       │   ├── __init__.py
│       │   └── main.py
│       └── tests/.keep
└── docs/
    ├── design/2026-05-29-guidearch-rewrite-design.md   # already exists
    └── plans/2026-05-30-m0-repo-bootstrap.md           # this file
```

---

## Phase A — Local repo + governance files

### Task 1: Initialize local git

**Files:** none yet (existing dir is empty besides `docs/`)

- [ ] **Step 1:** From `/Users/kaveh/repos/GuideArch/`, initialize git with `main` as the default branch:

```bash
git init -b main
```

- [ ] **Step 2:** Configure local repo identity (no global change):

```bash
git config user.name "Kaveh Razavi"
git config user.email "kaveh.razavi@gmail.com"
```

- [ ] **Step 3:** Verify:

```bash
git status
```

Expected output starts with `On branch main` and lists `docs/` as untracked.

---

### Task 2: Add `.gitignore`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.gitignore`

- [ ] **Step 1:** Create the file with this exact content:

```gitignore
# OS
.DS_Store
Thumbs.db
desktop.ini

# IDEs (selective)
.idea/
.vscode/*
!.vscode/settings.json
!.vscode/extensions.json
*.swp
*.swo

# AI / dev-tool artifacts — never commit
.claude/
.superpowers/
.aider*
.cursor/
.continue/
.windsurf/
.zed/
.copilot/
.tabnine/
CLAUDE.md
GEMINI.md
AGENTS.md
.mcp.json

# TypeScript / Node / Tauri
langs/typescript/node_modules/
langs/typescript/dist/
langs/typescript/build/
langs/typescript/.svelte-kit/
langs/typescript/src-tauri/target/
langs/typescript/src-tauri/gen/
langs/typescript/src-tauri/WixTools/
langs/typescript/.vite/
langs/typescript/*.log

# C# / .NET / Avalonia
langs/csharp/**/bin/
langs/csharp/**/obj/
langs/csharp/**/*.user
langs/csharp/**/.vs/
langs/csharp/**/*.suo
langs/csharp/**/publish/
langs/csharp/**/AppPackages/

# Python / uv / hatch
langs/python/.venv/
langs/python/**/__pycache__/
langs/python/**/.pytest_cache/
langs/python/**/.mypy_cache/
langs/python/**/.ruff_cache/
langs/python/dist/
langs/python/build/
langs/python/*.egg-info/
langs/python/.coverage
langs/python/htmlcov/

# Vendor / submodule content
# (vendor/vmx is a submodule — its content is tracked via .gitmodules + commit pin, not via files in this repo)
```

- [ ] **Step 2:** Commit:

```bash
git add .gitignore
git commit -m "chore: add .gitignore with AI tool artifact exclusions"
```

Verify the commit message contains no `Co-Authored-By` line:

```bash
git log -1 --pretty=full
```

---

### Task 3: Add `.gitattributes`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.gitattributes`

- [ ] **Step 1:** Create with content:

```
# Default: normalize line endings to LF
* text=auto eol=lf

# Force LF for shell, Python, TS, C#, YAML, JSON, MD
*.sh         text eol=lf
*.py         text eol=lf
*.ts         text eol=lf
*.tsx        text eol=lf
*.js         text eol=lf
*.cs         text eol=lf
*.csproj     text eol=lf
*.sln        text eol=lf
*.svelte     text eol=lf
*.yml        text eol=lf
*.yaml       text eol=lf
*.json       text eol=lf
*.md         text eol=lf
*.toml       text eol=lf

# Windows-only
*.bat        text eol=crlf
*.cmd        text eol=crlf
*.ps1        text eol=crlf

# Binary
*.png        binary
*.jpg        binary
*.ico        binary
*.icns       binary
*.dll        binary
*.so         binary
*.dylib      binary
*.exe        binary
```

- [ ] **Step 2:** Commit:

```bash
git add .gitattributes
git commit -m "chore: add .gitattributes for cross-platform line endings"
```

---

### Task 4: Add MIT `LICENSE`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/LICENSE`

- [ ] **Step 1:** Create with content:

```
MIT License

Copyright (c) 2026 Kaveh Razavi

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 2:** Commit:

```bash
git add LICENSE
git commit -m "docs: add MIT license"
```

---

### Task 5: Add `README.md` skeleton

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/README.md`

- [ ] **Step 1:** Create with content:

```markdown
# GuideArch

Modern fuzzy multi-criteria decision analysis for software architecture.

GuideArch helps software architects model a decision space — *decisions*, *alternatives*, quality *properties*, and *constraints* — and ranks the resulting candidate architectures using fuzzy TOPSIS. It identifies which decisions matter most (sensitivity analysis) and which constraints are most binding (elimination counting).

Three implementations of the same application share one language-neutral spec:

| Language | UI Framework | Desktop | Web |
|---|---|---|---|
| TypeScript | Svelte 5 + [Tauri 2](https://tauri.app) | ✓ | ✓ |
| C# | [Avalonia 12](https://avaloniaui.net) | ✓ | ✓ (WebAssembly) |
| Python | [NiceGUI 3.x](https://nicegui.io) | ✓ (pywebview) | ✓ |

All three are built on the [VMx](https://github.com/thekaveh/VMx) MVVM framework, included as a git submodule at `vendor/vmx/`.

## Status

Bootstrap (M0). Not yet usable. See [docs/design/](docs/design/) for the rewrite spec and [docs/plans/](docs/plans/) for the milestone plans.

## Repository layout

```
spec/                 language-neutral spec (schema, algorithms, conformance corpus, ADRs)
vendor/vmx/           VMx submodule
langs/typescript/     TS + Tauri + Svelte impl
langs/csharp/         C# + Avalonia impl
langs/python/         Python + NiceGUI impl
tools/                cross-cutting scripts
docs/                 design specs and milestone plans
```

## Quickstart (per impl)

Bootstrap stage — each impl runs a hello-world that imports VMx as a wiring proof.

### TypeScript

```bash
cd langs/typescript
pnpm install
pnpm dev          # browser
pnpm tauri dev    # desktop
```

### C#

```bash
cd langs/csharp
dotnet build
dotnet run --project src/GuideArch.View
```

### Python

```bash
cd langs/python
uv sync
uv run guidearch          # web
uv run guidearch --native # desktop (pywebview)
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All implementations must stay in conformance with `spec/conformance/`.

## License

MIT — see [LICENSE](LICENSE).
```

- [ ] **Step 2:** Commit:

```bash
git add README.md
git commit -m "docs: add README skeleton"
```

---

### Task 6: Add `CONTRIBUTING.md`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/CONTRIBUTING.md`

- [ ] **Step 1:** Create with content:

```markdown
# Contributing to GuideArch

Thanks for your interest in contributing. GuideArch is a spec-driven monorepo with three implementations kept in conformance by a shared corpus. The most important rule is: **the spec is the source of truth**, and **all three implementations must agree with it**.

## Before you start

- Read [docs/design/2026-05-29-guidearch-rewrite-design.md](docs/design/2026-05-29-guidearch-rewrite-design.md) for the architectural overview.
- Read [spec/ADRs/](spec/ADRs/) for the rationale behind specific decisions.
- Open an issue to discuss substantial changes before opening a PR.

## Workflow for a feature or fix

1. **Spec first.** If the change touches the domain or algorithm, update `spec/domain/` or `spec/algorithms/` first and, if relevant, add a new conformance scenario in `spec/conformance/scenarios/` with an expected output in `spec/conformance/expected/`.
2. **Implement in all three languages.** A feature is not "done" until TypeScript, C#, and Python all pass conformance for it.
3. **Use Conventional Commits.** Examples: `feat: add scenario comparison`, `fix(cs): off-by-one in topsis`.
4. **Open one PR.** Touching the spec and all three impls in one PR keeps reviewers oriented.

## Local development

- VMx is a git submodule at `vendor/vmx/`. Clone with `git clone --recurse-submodules`, or after cloning run `git submodule update --init`.
- Use `tools/use-vmx-local.sh` to consume VMx from the submodule (editable), or `tools/use-vmx-released.sh` to consume the published package versions.

## Tests

- Per-impl unit tests live under `langs/<impl>/tests/unit/`.
- Conformance tests live under `langs/<impl>/tests/conformance/` and consume `spec/conformance/`.
- CI runs both on every PR.

## Code style

- TypeScript: ESLint + Prettier — `pnpm lint && pnpm format`.
- C#: Treat warnings as errors. `dotnet format`.
- Python: ruff + mypy --strict — `uv run ruff check && uv run mypy`.

## Code of Conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
```

- [ ] **Step 2:** Commit:

```bash
git add CONTRIBUTING.md
git commit -m "docs: add CONTRIBUTING guide"
```

---

### Task 7: Add `CODE_OF_CONDUCT.md`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/CODE_OF_CONDUCT.md`

- [ ] **Step 1:** Fetch the official Contributor Covenant v2.1 text:

```bash
curl -fsSL https://www.contributor-covenant.org/version/2/1/code_of_conduct/code-of-conduct.md \
  -o CODE_OF_CONDUCT.md
```

- [ ] **Step 2:** Open the file and replace the placeholder `[INSERT CONTACT METHOD]` (appears once in the Enforcement section) with `kaveh.razavi@gmail.com`. Use:

```bash
sed -i.bak 's/\[INSERT CONTACT METHOD\]/kaveh.razavi@gmail.com/' CODE_OF_CONDUCT.md && rm CODE_OF_CONDUCT.md.bak
```

- [ ] **Step 3:** Verify the placeholder is gone:

```bash
grep -F "[INSERT CONTACT METHOD]" CODE_OF_CONDUCT.md || echo "OK — placeholder removed"
```

Expected: `OK — placeholder removed`.

- [ ] **Step 4:** Commit:

```bash
git add CODE_OF_CONDUCT.md
git commit -m "docs: add Contributor Covenant 2.1 code of conduct"
```

---

### Task 8: Add `SECURITY.md`

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/SECURITY.md`

- [ ] **Step 1:** Create with content:

```markdown
# Security Policy

## Supported Versions

GuideArch is pre-1.0 during bootstrap. Once v1.0.0 is released, the most recent minor version on the `main` branch is supported.

## Reporting a Vulnerability

Please report security issues privately by emailing **kaveh.razavi@gmail.com** with the subject line `[guidearch security]`.

Do **not** open a public GitHub issue for security reports.

You can expect:

- Acknowledgement within 72 hours.
- An initial assessment within one week.
- A coordinated disclosure timeline once the issue is understood.

Thank you for helping keep GuideArch and its users safe.
```

- [ ] **Step 2:** Commit:

```bash
git add SECURITY.md
git commit -m "docs: add security policy"
```

---

## Phase B — Spec skeleton + ADRs

### Task 9: Create `spec/` skeleton

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/spec/README.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/domain/.keep`
- Create: `/Users/kaveh/repos/GuideArch/spec/algorithms/.keep`
- Create: `/Users/kaveh/repos/GuideArch/spec/conformance/.keep`
- Create: `/Users/kaveh/repos/GuideArch/spec/conformance/scenarios/.keep`
- Create: `/Users/kaveh/repos/GuideArch/spec/conformance/expected/.keep`

- [ ] **Step 1:** Create `spec/README.md`:

```markdown
# GuideArch Spec

This directory is the language-neutral source of truth for GuideArch. Every implementation must conform to it.

## Layout

- `domain/` — scenario JSON schema, glossary, invariants. **Authored during M1.**
- `algorithms/` — formal write-ups of TOPSIS, critical-decisions, critical-constraints. **Authored during M1.**
- `viewmodels.md` — shared ViewModel tree shape (names, commands, observable properties). **Authored during M2.**
- `conformance/` — input scenarios + expected outputs + numerical tolerances. **Seeded during M1.**
- `ADRs/` — architecture decision records.

## Conformance contract

Each implementation under `langs/<impl>/tests/conformance/` reads every file in `conformance/scenarios/`, runs the impl's TOPSIS + analyses, and asserts that outputs match `conformance/expected/` within `conformance/tolerances.json`. CI fails any divergence.
```

- [ ] **Step 2:** Create empty `.keep` files for the subfolders:

```bash
mkdir -p spec/domain spec/algorithms spec/conformance/scenarios spec/conformance/expected
touch spec/domain/.keep spec/algorithms/.keep spec/conformance/.keep \
      spec/conformance/scenarios/.keep spec/conformance/expected/.keep
```

- [ ] **Step 3:** Commit:

```bash
git add spec/
git commit -m "docs(spec): scaffold spec/ skeleton for M1+"
```

---

### Task 10: Add ADR-0001 through ADR-0006

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0001-three-impls-vmx-submodule.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0002-json-schema-not-xml.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0003-topsis-no-msf.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0004-mit-license.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0005-single-monorepo-version.md`
- Create: `/Users/kaveh/repos/GuideArch/spec/ADRs/0006-nicegui-over-shiny.md`

- [ ] **Step 1:** Create `spec/ADRs/0001-three-impls-vmx-submodule.md`:

```markdown
# ADR-0001: Three implementations sharing one spec; VMx as submodule

**Status:** Accepted
**Date:** 2026-05-29

## Context

GuideArch was a Silverlight application. Silverlight is end-of-life. We want a modernized rewrite that simultaneously serves as a real-world demonstration of the VMx MVVM framework across all the languages VMx supports.

## Decision

Ship three concurrent implementations (TypeScript, C#, Python) of the same application, sharing one language-neutral spec under `spec/`. Vendor VMx as a git submodule at `vendor/vmx/` (pinned to a stable tag), consumed via local/editable installs during development and via published packages (npm / NuGet / PyPI) for downstream users.

## Consequences

- All three implementations must stay in conformance with the spec, enforced by CI.
- New features land in all three before any tag — this is a real scope multiplier (~3×) but the strongest cross-language proof of VMx.
- VMx improvements discovered during GuideArch work flow back upstream via PR against the VMx submodule.
- Single monorepo version (see ADR-0005) means release cadence is gated on the slowest impl.
```

- [ ] **Step 2:** Create `spec/ADRs/0002-json-schema-not-xml.md`:

```markdown
# ADR-0002: JSON Schema for scenario files (not legacy XML)

**Status:** Accepted
**Date:** 2026-05-29

## Context

The legacy GuideArch persisted scenarios as hand-rolled XML (e.g., `SAS.xml`, `EDS.xml`). XML works but is verbose, lacks first-class schema tooling across our three target languages, and the legacy format was undocumented.

## Decision

Adopt JSON as the scenario file format, with a JSON Schema at `spec/domain/scenario.schema.json` as the validation contract. Use string identifiers (e.g., `"d-database"`) instead of indices for cross-entity references. Provide a one-shot Python converter at `tools/import-legacy-xml.py` for porting legacy scenarios into the seed conformance corpus.

## Consequences

- All three implementations get free validation via mature JSON Schema libraries (`ajv`, `JsonSchema.Net`, `jsonschema`).
- The conformance corpus speaks a single format readable by all three impls without special handling.
- Legacy XML is read-only and only via the converter — the apps never load XML directly.
```

- [ ] **Step 3:** Create `spec/ADRs/0003-topsis-no-msf.md`:

```markdown
# ADR-0003: TOPSIS as in-repo code; no Microsoft Solver Foundation

**Status:** Accepted
**Date:** 2026-05-29

## Context

The legacy `.Old/GuideArch.Console` project references Microsoft Solver Foundation (MSF) but this reference is for a separate TSP demo. The actual GuideArch application uses a custom fuzzy TOPSIS algorithm implemented inline in `GuideArch.Model/Space.cs` — no external solver. MSF is also Windows/.NET-only and effectively unmaintained.

## Decision

Port the custom TOPSIS algorithm (ranking + critical-decisions + critical-constraints) directly into each implementation's `Models/Topsis/` namespace. No external constraint-solver dependency for v1. Document the algorithm formally in `spec/algorithms/topsis.md` during M1.

## Consequences

- No solver library is a runtime dependency.
- Each implementation owns a ~300-line TOPSIS port that conforms numerically to the spec.
- A pluggable solver backend (Z3 / MiniZinc / OR-Tools) is deferred to v1.3 for richer constraint kinds beyond threshold / dependency / conflict.
```

- [ ] **Step 4:** Create `spec/ADRs/0004-mit-license.md`:

```markdown
# ADR-0004: MIT License

**Status:** Accepted
**Date:** 2026-05-29

## Context

GuideArch is a small, academically-rooted tool. VMx (its primary dependency) is MIT-licensed. We evaluated MIT vs. Apache 2.0.

## Decision

Adopt the MIT License. Match VMx; keep license text minimal; preserve the option to relicense to Apache 2.0 later if contributor base or patent exposure change.

## Consequences

- One-off contributors face a ~170-word license rather than ~10,000 words.
- No explicit patent grant or retaliation clause. We accept this risk for a non-patent-heavy domain.
- Apache-2.0 dependencies (Tauri, Avalonia, NiceGUI) can be freely consumed under MIT — license compatibility is one-way fine.
- Future relicense to Apache 2.0 requires sole-copyright or sign-off from all contributors.
```

- [ ] **Step 5:** Create `spec/ADRs/0005-single-monorepo-version.md`:

```markdown
# ADR-0005: Single monorepo version; all three impls release together

**Status:** Accepted
**Date:** 2026-05-29

## Context

A tri-impl monorepo can either ship per-language versions (`ts-v1.0.0`, `cs-v1.0.0`, …) or a single unified version (`v1.0.0` releases all three).

## Decision

Single monorepo version. One `v<major>.<minor>.<patch>` tag publishes all three implementations. No release until all three pass conformance and a manual smoke test.

## Consequences

- Easier to reason about parity: "what features are in v1.2?" has one answer.
- One CHANGELOG.
- Release cadence is gated on the slowest impl. If C# is a sprint behind, the whole release waits.
- If the gating becomes a recurring problem post-v1, switch to per-lang versions in an ADR amendment.
```

- [ ] **Step 6:** Create `spec/ADRs/0006-nicegui-over-shiny.md`:

```markdown
# ADR-0006: NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)

**Status:** Accepted
**Date:** 2026-05-29

## Context

We need a Python web/desktop UI framework that pairs well with VMx-Python and yields a professional-looking application. Candidates considered: Streamlit, Shiny for Python, NiceGUI, Flet, PySide6, Textual.

- **Streamlit** reruns the entire script on every interaction; state lives in `st.session_state`. Fundamentally incompatible with MVVM observable semantics.
- **Shiny for Python** has a reactive graph and persistent state but accesses inputs anonymously (`input.x()`) rather than as bindable properties on objects.
- **NiceGUI 3.x** ships `bindable_property`, `@bindable_dataclass`, and a new `Event` class explicitly designed to "synchronize long-living objects with short-living UI." This is the MVVM bridge VMx wants.
- NiceGUI's `ui.run(native=True)` opens a pywebview window using the OS webview (not bundled Chromium), enabling the same Python code to ship as desktop and web — symmetric with the TS+Tauri and C#+Avalonia stories.
- Quasar (Vue 3 / Material Design 3) + Tailwind 4 produce a polished baseline.

## Decision

Adopt NiceGUI 3.x as the Python view layer. Build a small VMx ↔ NiceGUI binding adapter at `langs/python/src/guidearch/view/adapters/`.

## Consequences

- The Python impl reaches feature parity with TS+Tauri and C#+Avalonia on deployment surface (desktop + web).
- We write a ~30–50 LOC adapter mapping VMx `PropertyChangedMessage` to NiceGUI binding propagation; contributable back to VMx as `vmx.bindings.nicegui`.
- We accept NiceGUI's documentation being less comprehensive than Shiny's — the GitHub Discussions forum is the supplement.
- Standalone executable packaging (PyInstaller `--onefile`) has known multiprocessing edge cases; deferred past v1.0.
```

- [ ] **Step 7:** Commit:

```bash
git add spec/ADRs/
git commit -m "docs(spec): add initial ADRs (0001-0006)"
```

---

## Phase C — VMx submodule

### Task 11: Add VMx as a git submodule

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.gitmodules`
- Add: `/Users/kaveh/repos/GuideArch/vendor/vmx/` (submodule)

- [ ] **Step 1:** Add the submodule pointing at the user's VMx repo:

```bash
git submodule add https://github.com/thekaveh/VMx.git vendor/vmx
```

- [ ] **Step 2:** Pin the submodule to the `v2.1.0` tag. From the repo root:

```bash
cd vendor/vmx
git fetch --tags
git checkout v2.1.0
cd ../..
```

If the tag does not exist on the remote (the user may not have tagged yet), fall back to pinning to the current `main` HEAD commit and record the commit SHA in the commit message. Resolve by asking the user to add a `v2.1.0` tag to the VMx repo before merge.

**NOTE (post-execution):** The fallback path was taken at M0. The submodule was pinned to commit `e2b23f8` (described as `python-v1.0.0-66-ge2b23f8`), and the commit message used was `build: vendor VMx as submodule at e2b23f8 (v2.1.0 tag not yet present upstream)`.

- [ ] **Step 3:** Verify `.gitmodules` content:

```bash
cat .gitmodules
```

Expected:

```
[submodule "vendor/vmx"]
	path = vendor/vmx
	url = https://github.com/thekaveh/VMx.git
```

- [ ] **Step 4:** Commit:

```bash
git add .gitmodules vendor/vmx
git commit -m "build: vendor VMx as submodule pinned to v2.1.0"
```

---

## Phase D — tools/

### Task 12: Add VMx mode-switch scripts + legacy import stub

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/tools/use-vmx-local.sh`
- Create: `/Users/kaveh/repos/GuideArch/tools/use-vmx-released.sh`
- Create: `/Users/kaveh/repos/GuideArch/tools/import-legacy-xml.py`

- [ ] **Step 1:** Create `tools/use-vmx-local.sh`:

```bash
#!/usr/bin/env bash
# Switch all three impls to consume VMx from the local submodule (editable).
# Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "→ TypeScript: pointing package.json at vendor/vmx/langs/typescript"
# To be wired in M0 Task 18 (TS scaffold). For now, just print.
node -e 'const p=require("./langs/typescript/package.json"); p.dependencies=p.dependencies||{}; p.dependencies.vmx="file:../../vendor/vmx/langs/typescript"; require("fs").writeFileSync("./langs/typescript/package.json", JSON.stringify(p,null,2)+"\n")' \
  2>/dev/null && echo "  ok" || echo "  (skipped — TS scaffold not present yet)"

echo "→ C#: pointing ViewModels.csproj at vendor/vmx/langs/csharp/src/VMx/VMx.csproj"
# Handled by Directory.Build.props variable in M0 Task 22.
echo "  (set VMxSource=local in Directory.Build.props)"

echo "→ Python: editable install from vendor/vmx/langs/python"
if [[ -f langs/python/pyproject.toml ]]; then
  cd langs/python
  uv pip install -e ../../vendor/vmx/langs/python
  cd "$ROOT"
  echo "  ok"
else
  echo "  (skipped — Python scaffold not present yet)"
fi

echo "Done. VMx is now consumed locally from vendor/vmx/."
```

- [ ] **Step 2:** Create `tools/use-vmx-released.sh`:

```bash
#!/usr/bin/env bash
# Switch all three impls back to consuming VMx from published packages.
# Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VMX_VERSION="${VMX_VERSION:-2.1.0}"

echo "→ TypeScript: pointing package.json at npm vmx@${VMX_VERSION}"
node -e "const p=require('./langs/typescript/package.json'); p.dependencies=p.dependencies||{}; p.dependencies.vmx='^${VMX_VERSION}'; require('fs').writeFileSync('./langs/typescript/package.json', JSON.stringify(p,null,2)+'\n')" \
  2>/dev/null && echo "  ok" || echo "  (skipped — TS scaffold not present yet)"

echo "→ C#: setting VMxSource=released in Directory.Build.props"
echo "  (manually toggle <VMxSource>released</VMxSource> in Directory.Build.props)"

echo "→ Python: install vmx==${VMX_VERSION} from PyPI"
if [[ -f langs/python/pyproject.toml ]]; then
  cd langs/python
  uv pip install "vmx==${VMX_VERSION}"
  cd "$ROOT"
  echo "  ok"
else
  echo "  (skipped — Python scaffold not present yet)"
fi

echo "Done. VMx is now consumed from published packages (${VMX_VERSION})."
```

- [ ] **Step 3:** Make both executable:

```bash
chmod +x tools/use-vmx-local.sh tools/use-vmx-released.sh
```

- [ ] **Step 4:** Create `tools/import-legacy-xml.py` as a stub:

```python
"""
One-shot importer: legacy GuideArch XML scenarios → JSON.

This script is a stub at M0. Full implementation is part of M1, when the JSON
schema in `spec/domain/scenario.schema.json` is finalized.

Usage (M1+):
    python tools/import-legacy-xml.py <path/to/legacy.xml> -o spec/conformance/scenarios/<name>.json
"""
from __future__ import annotations

import sys


def main() -> int:
    print("tools/import-legacy-xml.py: not yet implemented (M1 work).", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 5:** Commit:

```bash
git add tools/
git commit -m "build(tools): add VMx mode-switch scripts and legacy XML import stub"
```

---

## Phase E — TypeScript scaffold

### Task 13: Initialize TypeScript scaffold with Vite + Svelte 5 + Tauri 2

**Files (created by the scaffold command + edits):**
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/package.json`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/tsconfig.json`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/vite.config.ts`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/svelte.config.js`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/index.html`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/src/main.ts`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/src/App.svelte`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/src/app.css`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/src/vite-env.d.ts`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/src-tauri/` (full Tauri 2 shell)

- [ ] **Step 1:** Ensure pnpm and Rust are installed (prereq for Tauri 2). If not:

```bash
which pnpm || npm install -g pnpm@latest
which cargo || (echo "Install Rust from https://rustup.rs" && exit 1)
which tauri || cargo install create-tauri-app --locked
```

- [ ] **Step 2:** From repo root, scaffold a Tauri 2 + Svelte + TypeScript project into `langs/typescript/`:

```bash
mkdir -p langs/typescript
cd langs/typescript
pnpm create tauri-app@latest . --identifier com.thekaveh.guidearch --frontend svelte-ts --manager pnpm --tauri-version 2 --yes
```

If the interactive prompt cannot be fully bypassed by flags in your `create-tauri-app` version, run interactively and answer:
- Project name: `GuideArch` (or accept default)
- Identifier: `com.thekaveh.guidearch`
- Manager: `pnpm`
- UI: `Svelte` with TypeScript
- Use Tauri 2: yes

- [ ] **Step 3:** Replace `package.json` with this minimal version (the scaffold's default is fine to start from; edit `name` and `version`):

```bash
node -e '
const fs=require("fs");
const p=JSON.parse(fs.readFileSync("package.json","utf8"));
p.name="@guidearch/typescript";
p.version="0.0.0";
p.private=true;
p.description="GuideArch TypeScript + Tauri + Svelte implementation";
p.license="MIT";
p.dependencies=p.dependencies||{};
p.dependencies.vmx="file:../../vendor/vmx/langs/typescript";
fs.writeFileSync("package.json", JSON.stringify(p,null,2)+"\n");
'
```

- [ ] **Step 4:** Install deps:

```bash
pnpm install
```

- [ ] **Step 5:** Replace `src/App.svelte` with a "hello VMx" component:

```svelte
<script lang="ts">
  import { helloVMx } from './lib/hello-vmx';

  let greeting = $state(helloVMx());
</script>

<main>
  <h1>GuideArch</h1>
  <p class="subtitle">TypeScript + Tauri + Svelte 5 implementation — bootstrap</p>
  <p class="vmx-proof">{greeting}</p>
</main>

<style>
  main { font-family: system-ui, sans-serif; padding: 3rem; max-width: 720px; margin: 0 auto; }
  h1 { font-size: 2.5rem; margin: 0 0 .5rem; }
  .subtitle { color: #666; margin: 0 0 2rem; }
  .vmx-proof { padding: 1rem; background: #f4f4f4; border-radius: 4px; font-family: monospace; }
</style>
```

- [ ] **Step 6:** Create `src/lib/hello-vmx.ts` proving VMx imports work:

```typescript
import { ComponentVM } from 'vmx';

/**
 * Smoke-test VMx wiring by instantiating a trivial ComponentVM and
 * returning a descriptive string for the UI.
 */
export function helloVMx(): string {
  const vm = new ComponentVM<{ message: string }>({ message: 'hello from VMx' });
  vm.construct();
  return `VMx loaded — model.message = "${vm.model.message}", status = ${vm.constructionStatus}`;
}
```

- [ ] **Step 7:** Run the dev server in the browser to verify:

```bash
pnpm dev
```

Open `http://localhost:1420` (or whichever port Vite reports). Expected: page renders "GuideArch" + "VMx loaded — model.message = …, status = Constructed". Stop with Ctrl-C.

- [ ] **Step 8:** From the repo root, commit the TS scaffold (the `.gitignore` excludes `node_modules/`, `dist/`, etc.):

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/typescript/
git commit -m "feat(ts): scaffold Tauri 2 + Svelte 5 hello-VMx app"
```

---

### Task 14: Add ESLint + Prettier for TypeScript

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/.eslintrc.cjs`
- Create: `/Users/kaveh/repos/GuideArch/langs/typescript/.prettierrc`
- Modify: `/Users/kaveh/repos/GuideArch/langs/typescript/package.json` (add scripts + devDeps)

- [ ] **Step 1:** From `langs/typescript/`:

```bash
cd langs/typescript
pnpm add -D eslint @typescript-eslint/parser @typescript-eslint/eslint-plugin eslint-plugin-svelte svelte-eslint-parser prettier prettier-plugin-svelte
```

- [ ] **Step 2:** Create `.eslintrc.cjs`:

```javascript
module.exports = {
  root: true,
  parser: '@typescript-eslint/parser',
  parserOptions: { ecmaVersion: 2022, sourceType: 'module', extraFileExtensions: ['.svelte'] },
  plugins: ['@typescript-eslint'],
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:svelte/recommended',
  ],
  overrides: [
    { files: ['*.svelte'], parser: 'svelte-eslint-parser', parserOptions: { parser: '@typescript-eslint/parser' } },
  ],
  ignorePatterns: ['dist/', 'node_modules/', 'src-tauri/target/'],
};
```

- [ ] **Step 3:** Create `.prettierrc`:

```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "all",
  "printWidth": 100,
  "tabWidth": 2,
  "plugins": ["prettier-plugin-svelte"]
}
```

- [ ] **Step 4:** Add scripts to `package.json`:

```bash
node -e '
const fs=require("fs");
const p=JSON.parse(fs.readFileSync("package.json","utf8"));
p.scripts=p.scripts||{};
p.scripts.lint="eslint src --ext .ts,.svelte";
p.scripts.format="prettier --write src";
p.scripts["format:check"]="prettier --check src";
fs.writeFileSync("package.json", JSON.stringify(p,null,2)+"\n");
'
```

- [ ] **Step 5:** Verify lint + format pass:

```bash
pnpm lint
pnpm format:check
```

Expected: both exit 0. If `format:check` fails, run `pnpm format` and re-check.

- [ ] **Step 6:** Commit from repo root:

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/typescript/.eslintrc.cjs langs/typescript/.prettierrc langs/typescript/package.json langs/typescript/pnpm-lock.yaml
git commit -m "chore(ts): add eslint + prettier with strict configs"
```

---

## Phase F — C# scaffold

### Task 15: Create C# solution + Models project

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/GuideArch.sln`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/Directory.Build.props`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.Models/GuideArch.Models.csproj`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.Models/HelloVmx.cs`

- [ ] **Step 1:** Verify .NET 8 SDK is installed:

```bash
dotnet --list-sdks | grep "^8\." || (echo "Install .NET 8 SDK from https://dotnet.microsoft.com" && exit 1)
```

- [ ] **Step 2:** Create the solution and project structure:

```bash
mkdir -p langs/csharp/src
cd langs/csharp
dotnet new sln -n GuideArch
mkdir -p src/GuideArch.Models
cd src/GuideArch.Models
dotnet new classlib -n GuideArch.Models -o . --framework net8.0
cd ../..
dotnet sln GuideArch.sln add src/GuideArch.Models/GuideArch.Models.csproj
```

- [ ] **Step 3:** Create `Directory.Build.props` at `langs/csharp/Directory.Build.props` with shared settings + VMx source toggle:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Toggle: 'local' uses vendor/vmx submodule; 'released' uses NuGet -->
    <VMxSource Condition="'$(VMxSource)'==''">local</VMxSource>
    <VMxVersion>2.1.0</VMxVersion>
  </PropertyGroup>

  <ItemGroup Condition="'$(VMxSource)'=='local'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)../../vendor/vmx/langs/csharp/src/VMx/VMx.csproj" />
  </ItemGroup>

  <ItemGroup Condition="'$(VMxSource)'=='released'">
    <PackageReference Include="VMx" Version="$(VMxVersion)" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4:** Replace `src/GuideArch.Models/Class1.cs` (created by `dotnet new classlib`) with `HelloVmx.cs`:

```bash
rm src/GuideArch.Models/Class1.cs
```

Then create `src/GuideArch.Models/HelloVmx.cs`:

```csharp
using VMx;

namespace GuideArch.Models;

/// <summary>
/// Smoke-test VMx wiring by instantiating a trivial ComponentVM and
/// returning a descriptive string for the UI.
/// </summary>
public static class HelloVmx
{
    public sealed record Greeting(string Message);

    public static string Run()
    {
        var vm = new ComponentVM<Greeting>(new Greeting("hello from VMx"));
        vm.Construct();
        return $"VMx loaded — model.Message = \"{vm.Model.Message}\", status = {vm.ConstructionStatus}";
    }
}
```

- [ ] **Step 5:** Build to verify VMx ProjectReference resolves:

```bash
dotnet build GuideArch.sln
```

Expected: build succeeds with 0 warnings, 0 errors.

If the build fails because `vendor/vmx/langs/csharp/src/VMx/VMx.csproj` path differs, inspect the VMx submodule and adjust the `ProjectReference Include=` path in `Directory.Build.props`.

- [ ] **Step 6:** Commit from repo root:

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/csharp/
git commit -m "feat(cs): scaffold solution and Models project with VMx project reference"
```

---

### Task 16: Add ViewModels project

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.ViewModels/GuideArch.ViewModels.csproj`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.ViewModels/HelloVmxVM.cs`

- [ ] **Step 1:** Create the ViewModels project:

```bash
cd langs/csharp
mkdir -p src/GuideArch.ViewModels
cd src/GuideArch.ViewModels
dotnet new classlib -n GuideArch.ViewModels -o . --framework net8.0
rm Class1.cs
cd ../..
dotnet sln GuideArch.sln add src/GuideArch.ViewModels/GuideArch.ViewModels.csproj
dotnet add src/GuideArch.ViewModels/GuideArch.ViewModels.csproj reference src/GuideArch.Models/GuideArch.Models.csproj
```

- [ ] **Step 2:** Create `src/GuideArch.ViewModels/HelloVmxVM.cs`:

```csharp
using GuideArch.Models;
using VMx;

namespace GuideArch.ViewModels;

public sealed class HelloVmxVM : ComponentVM<HelloVmx.Greeting>
{
    public HelloVmxVM() : base(new HelloVmx.Greeting(HelloVmx.Run())) { }
}
```

- [ ] **Step 3:** Build:

```bash
dotnet build GuideArch.sln
```

Expected: success.

- [ ] **Step 4:** Commit from repo root:

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/csharp/
git commit -m "feat(cs): add GuideArch.ViewModels project"
```

---

### Task 17: Add View Avalonia project

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/GuideArch.View.csproj`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/Program.cs`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/App.axaml`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/App.axaml.cs`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/MainWindow.axaml`
- Create: `/Users/kaveh/repos/GuideArch/langs/csharp/src/GuideArch.View/MainWindow.axaml.cs`

- [ ] **Step 1:** Install Avalonia templates if not already:

```bash
dotnet new install Avalonia.Templates
```

- [ ] **Step 2:** Create the View project from the Avalonia desktop template:

```bash
cd langs/csharp
mkdir -p src/GuideArch.View
cd src/GuideArch.View
dotnet new avalonia.app -n GuideArch.View -o . --framework net8.0
cd ../..
dotnet sln GuideArch.sln add src/GuideArch.View/GuideArch.View.csproj
dotnet add src/GuideArch.View/GuideArch.View.csproj reference src/GuideArch.ViewModels/GuideArch.ViewModels.csproj
```

- [ ] **Step 3:** Replace `src/GuideArch.View/MainWindow.axaml` with VMx-wired markup:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:GuideArch.ViewModels"
        x:Class="GuideArch.View.MainWindow"
        x:DataType="vm:HelloVmxVM"
        Title="GuideArch"
        Width="720" Height="480">
  <Window.DataContext>
    <vm:HelloVmxVM />
  </Window.DataContext>

  <StackPanel Margin="48" Spacing="12">
    <TextBlock Text="GuideArch" FontSize="32" FontWeight="Bold" />
    <TextBlock Text="C# + Avalonia 12 implementation — bootstrap"
               Foreground="#666" />
    <Border Background="#f4f4f4" Padding="16" CornerRadius="4">
      <TextBlock Text="{Binding Model.Message}" FontFamily="Cascadia Code,Consolas,monospace" />
    </Border>
  </StackPanel>
</Window>
```

- [ ] **Step 4:** Confirm `src/GuideArch.View/MainWindow.axaml.cs` is the default:

```csharp
using Avalonia.Controls;

namespace GuideArch.View;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 5:** Build:

```bash
dotnet build GuideArch.sln
```

Expected: success.

- [ ] **Step 6:** Run the desktop app to verify:

```bash
dotnet run --project src/GuideArch.View
```

Expected: window opens titled "GuideArch" showing the VMx greeting. Close it.

- [ ] **Step 7:** Commit from repo root:

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/csharp/
git commit -m "feat(cs): scaffold Avalonia View project with VMx-bound hello window"
```

---

## Phase G — Python scaffold

### Task 18: Initialize Python scaffold with NiceGUI 3.x + pywebview

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/langs/python/pyproject.toml`
- Create: `/Users/kaveh/repos/GuideArch/langs/python/src/guidearch/__init__.py`
- Create: `/Users/kaveh/repos/GuideArch/langs/python/src/guidearch/main.py`
- Create: `/Users/kaveh/repos/GuideArch/langs/python/src/guidearch/hello_vmx.py`
- Create: `/Users/kaveh/repos/GuideArch/langs/python/README.md`

- [ ] **Step 1:** Verify Python 3.12 + uv:

```bash
python3 --version | grep -E "3\.(12|13)" || (echo "Install Python 3.12+" && exit 1)
which uv || curl -LsSf https://astral.sh/uv/install.sh | sh
```

- [ ] **Step 2:** Create `langs/python/pyproject.toml`:

```toml
[project]
name = "guidearch"
version = "0.0.0"
description = "GuideArch — Python + NiceGUI implementation"
readme = "README.md"
license = { text = "MIT" }
requires-python = ">=3.11"
authors = [{ name = "Kaveh Razavi", email = "kaveh.razavi@gmail.com" }]
dependencies = [
    "nicegui>=3.0.0",
    "pywebview>=5.0",
    "vmx>=2.1.0",
]

[project.optional-dependencies]
dev = [
    "pytest>=8",
    "pytest-cov>=5",
    "mypy>=1.10",
    "ruff>=0.6",
]

[project.scripts]
guidearch = "guidearch.main:main"

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.hatch.build.targets.wheel]
packages = ["src/guidearch"]

[tool.ruff]
line-length = 100
target-version = "py311"

[tool.ruff.lint]
select = ["E", "F", "I", "B", "UP", "SIM", "RUF"]

[tool.mypy]
python_version = "3.11"
strict = true
warn_unused_ignores = true
warn_redundant_casts = true

[tool.uv.sources]
# Use the vendored VMx by default. Toggle to released via tools/use-vmx-released.sh.
vmx = { path = "../../vendor/vmx/langs/python", editable = true }
```

- [ ] **Step 3:** Create `langs/python/README.md`:

```markdown
# GuideArch — Python

Python + NiceGUI 3.x implementation of GuideArch.

## Dev setup

```bash
cd langs/python
uv sync
```

## Run

```bash
uv run guidearch          # web (browser, default port 8080)
uv run guidearch --native # desktop (pywebview window)
```
```

- [ ] **Step 4:** Create `langs/python/src/guidearch/__init__.py`:

```python
"""GuideArch — Python implementation."""

__version__ = "0.0.0"
```

- [ ] **Step 5:** Create `langs/python/src/guidearch/hello_vmx.py`:

```python
"""Smoke-test VMx wiring by instantiating a trivial ComponentVM."""
from __future__ import annotations

from dataclasses import dataclass

from vmx import ComponentVM


@dataclass(frozen=True)
class Greeting:
    message: str


def hello_vmx() -> str:
    vm: ComponentVM[Greeting] = ComponentVM(Greeting("hello from VMx"))
    vm.construct()
    return (
        f'VMx loaded — model.message = "{vm.model.message}", '
        f"status = {vm.construction_status.name}"
    )
```

- [ ] **Step 6:** Create `langs/python/src/guidearch/main.py`:

```python
"""GuideArch Python entrypoint — NiceGUI hello-VMx UI."""
from __future__ import annotations

import argparse

from nicegui import ui

from .hello_vmx import hello_vmx


def build_ui() -> None:
    with ui.column().classes("max-w-3xl mx-auto p-12 gap-3"):
        ui.label("GuideArch").classes("text-4xl font-bold")
        ui.label("Python + NiceGUI 3.x implementation — bootstrap").classes(
            "text-gray-500"
        )
        ui.label(hello_vmx()).classes(
            "p-4 bg-gray-100 rounded font-mono text-sm"
        )


def main() -> None:
    parser = argparse.ArgumentParser(prog="guidearch")
    parser.add_argument(
        "--native",
        action="store_true",
        help="open in a native pywebview window instead of a browser",
    )
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()

    build_ui()
    ui.run(
        title="GuideArch",
        native=args.native,
        port=args.port,
        reload=False,
        show=not args.native,
    )


if __name__ in {"__main__", "__mp_main__"}:
    main()
```

- [ ] **Step 7:** Sync deps:

```bash
cd langs/python
uv sync
```

Expected: VMx installs in editable mode from `vendor/vmx/langs/python`; NiceGUI 3.x and pywebview install from PyPI.

- [ ] **Step 8:** Smoke-test the web mode:

```bash
uv run guidearch --port 8765 &
PID=$!
sleep 3
curl -fsS http://localhost:8765 >/dev/null && echo "OK — web mode reachable"
kill $PID
```

Expected: `OK — web mode reachable`.

- [ ] **Step 9:** (Manual) Smoke-test native mode locally — run `uv run guidearch --native`, confirm a pywebview window opens showing the VMx greeting, then close it. Skip on CI (no display).

- [ ] **Step 10:** Lint + type-check:

```bash
uv run ruff check src
uv run mypy src
```

Expected: both clean.

- [ ] **Step 11:** Commit from repo root:

```bash
cd /Users/kaveh/repos/GuideArch
git add langs/python/
git commit -m "feat(py): scaffold NiceGUI 3.x hello-VMx app with native mode"
```

---

## Phase H — GitHub Actions CI

### Task 19: Add TypeScript CI workflow

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/typescript.yml`

- [ ] **Step 1:** Create with content:

```yaml
name: typescript

on:
  push:
    branches: [main]
    paths:
      - 'langs/typescript/**'
      - 'vendor/vmx/**'
      - '.github/workflows/typescript.yml'
  pull_request:
    paths:
      - 'langs/typescript/**'
      - 'vendor/vmx/**'
      - '.github/workflows/typescript.yml'
  workflow_dispatch:

jobs:
  build:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    defaults:
      run:
        working-directory: langs/typescript
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - uses: pnpm/action-setup@v4
        with:
          version: latest

      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'pnpm'
          cache-dependency-path: langs/typescript/pnpm-lock.yaml

      - name: Install Linux Tauri prereqs
        if: matrix.os == 'ubuntu-latest'
        run: |
          sudo apt-get update
          sudo apt-get install -y libwebkit2gtk-4.1-dev libsoup-3.0-dev \
            build-essential curl wget file libxdo-dev libssl-dev \
            libayatana-appindicator3-dev librsvg2-dev

      - name: Install
        run: pnpm install --frozen-lockfile

      - name: Lint
        run: pnpm lint

      - name: Format check
        run: pnpm format:check

      - name: Build (web)
        run: pnpm build
```

- [ ] **Step 2:** Commit:

```bash
git add .github/workflows/typescript.yml
git commit -m "ci(ts): add lint + format + build workflow across OS matrix"
```

---

### Task 20: Add C# CI workflow

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/csharp.yml`

- [ ] **Step 1:** Create with content:

```yaml
name: csharp

on:
  push:
    branches: [main]
    paths:
      - 'langs/csharp/**'
      - 'vendor/vmx/**'
      - '.github/workflows/csharp.yml'
  pull_request:
    paths:
      - 'langs/csharp/**'
      - 'vendor/vmx/**'
      - '.github/workflows/csharp.yml'
  workflow_dispatch:

jobs:
  build:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    defaults:
      run:
        working-directory: langs/csharp
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore GuideArch.sln

      - name: Build
        run: dotnet build GuideArch.sln --configuration Release --no-restore

      - name: Format check
        run: dotnet format GuideArch.sln --verify-no-changes --no-restore
```

- [ ] **Step 2:** Commit:

```bash
git add .github/workflows/csharp.yml
git commit -m "ci(cs): add build + format-check workflow across OS matrix"
```

---

### Task 21: Add Python CI workflow

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/python.yml`

- [ ] **Step 1:** Create with content:

```yaml
name: python

on:
  push:
    branches: [main]
    paths:
      - 'langs/python/**'
      - 'vendor/vmx/**'
      - '.github/workflows/python.yml'
  pull_request:
    paths:
      - 'langs/python/**'
      - 'vendor/vmx/**'
      - '.github/workflows/python.yml'
  workflow_dispatch:

jobs:
  build:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]
        python: ['3.11', '3.12', '3.13']
    runs-on: ${{ matrix.os }}
    defaults:
      run:
        working-directory: langs/python
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive

      - uses: actions/setup-python@v5
        with:
          python-version: ${{ matrix.python }}

      - uses: astral-sh/setup-uv@v3

      - name: Sync
        run: uv sync --all-extras

      - name: Ruff
        run: uv run ruff check src

      - name: Mypy
        run: uv run mypy src

      - name: Smoke test (web mode reachable)
        if: matrix.os == 'ubuntu-latest'
        run: |
          uv run guidearch --port 8765 &
          PID=$!
          sleep 5
          curl -fsS http://localhost:8765 >/dev/null
          kill $PID
```

- [ ] **Step 2:** Commit:

```bash
git add .github/workflows/python.yml
git commit -m "ci(py): add ruff + mypy + smoke-test workflow across OS+Python matrix"
```

---

### Task 22: Add spec.yml + conformance.yml (stubs) + vmx-bump.yml

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/spec.yml`
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/conformance.yml`
- Create: `/Users/kaveh/repos/GuideArch/.github/workflows/vmx-bump.yml`

- [ ] **Step 1:** Create `spec.yml`:

```yaml
name: spec

on:
  push:
    branches: [main]
    paths: ['spec/**', '.github/workflows/spec.yml']
  pull_request:
    paths: ['spec/**', '.github/workflows/spec.yml']
  workflow_dispatch:

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Validate JSON files parse
        run: |
          set -e
          if find spec -name '*.json' | read; then
            find spec -name '*.json' -exec sh -c 'python3 -c "import json,sys; json.load(open(sys.argv[1]))" "$1"' _ {} \;
            echo "All spec/*.json files parse."
          else
            echo "No JSON files in spec/ yet (M1+ will populate)."
          fi

      - name: Check ADR filenames are well-formed
        run: |
          set -e
          for f in spec/ADRs/*.md; do
            base=$(basename "$f")
            if ! [[ "$base" =~ ^[0-9]{4}-[a-z0-9-]+\.md$ ]]; then
              echo "Malformed ADR filename: $base" >&2
              exit 1
            fi
          done
          echo "All ADR filenames are well-formed."
```

- [ ] **Step 2:** Create `conformance.yml` (stub for M0; expanded in M1):

```yaml
name: conformance

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

jobs:
  status:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Conformance gate
        run: |
          if [ -d spec/conformance/scenarios ] && [ "$(ls -A spec/conformance/scenarios 2>/dev/null | grep -v '^\.keep$' || true)" ]; then
            echo "Conformance scenarios present but cross-impl runner not yet implemented (M1)." >&2
            exit 0  # do not fail bootstrap CI
          else
            echo "No conformance scenarios yet (M1 will seed them). Bootstrap conformance: pass."
          fi
```

- [ ] **Step 3:** Create `vmx-bump.yml`:

```yaml
name: vmx-bump

on:
  schedule:
    - cron: '0 8 * * 1'  # Mondays at 08:00 UTC
  workflow_dispatch:

jobs:
  check:
    runs-on: ubuntu-latest
    permissions:
      contents: write
      issues: write
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: Check for newer VMx tag
        id: check
        run: |
          cd vendor/vmx
          git fetch --tags
          CURRENT=$(git describe --tags --exact-match 2>/dev/null || echo "unknown")
          LATEST=$(git tag --list 'v*.*.*' | sort -V | tail -n1)
          echo "current=$CURRENT" >> "$GITHUB_OUTPUT"
          echo "latest=$LATEST" >> "$GITHUB_OUTPUT"
          if [ "$CURRENT" != "$LATEST" ]; then
            echo "needs_bump=true" >> "$GITHUB_OUTPUT"
          else
            echo "needs_bump=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Open issue if bump available
        if: steps.check.outputs.needs_bump == 'true'
        uses: actions/github-script@v7
        with:
          script: |
            const current = '${{ steps.check.outputs.current }}';
            const latest = '${{ steps.check.outputs.latest }}';
            const title = `chore(vmx): bump submodule from ${current} to ${latest}`;
            const issues = await github.rest.issues.listForRepo({
              owner: context.repo.owner, repo: context.repo.repo,
              state: 'open', labels: 'vmx-bump'
            });
            if (issues.data.find(i => i.title === title)) {
              core.info('Issue already open.');
              return;
            }
            await github.rest.issues.create({
              owner: context.repo.owner, repo: context.repo.repo,
              title,
              labels: ['vmx-bump'],
              body: `VMx submodule is pinned at \`${current}\`, but \`${latest}\` is available.\n\nTo bump:\n\`\`\`bash\ncd vendor/vmx && git checkout ${latest} && cd ../..\ngit add vendor/vmx && git commit -m "build: bump VMx submodule to ${latest}"\n\`\`\``
            });
```

- [ ] **Step 4:** Commit:

```bash
git add .github/workflows/spec.yml .github/workflows/conformance.yml .github/workflows/vmx-bump.yml
git commit -m "ci: add spec validation, conformance stub, and weekly VMx-bump check"
```

---

## Phase I — GitHub scaffolding

### Task 23: Add issue + PR templates and dependabot

**Files:**
- Create: `/Users/kaveh/repos/GuideArch/.github/ISSUE_TEMPLATE/bug.md`
- Create: `/Users/kaveh/repos/GuideArch/.github/ISSUE_TEMPLATE/feature.md`
- Create: `/Users/kaveh/repos/GuideArch/.github/ISSUE_TEMPLATE/conformance-divergence.md`
- Create: `/Users/kaveh/repos/GuideArch/.github/ISSUE_TEMPLATE/question.md`
- Create: `/Users/kaveh/repos/GuideArch/.github/PULL_REQUEST_TEMPLATE.md`
- Create: `/Users/kaveh/repos/GuideArch/.github/dependabot.yml`

- [ ] **Step 1:** Create `.github/ISSUE_TEMPLATE/bug.md`:

```markdown
---
name: Bug report
about: Report incorrect behavior in one or more implementations
labels: bug
---

**Impl(s) affected:** <!-- typescript / csharp / python / spec / all -->

**What happened:**

**What you expected:**

**Steps to reproduce:**

1.
2.
3.

**Environment:**

- OS:
- VMx submodule SHA / tag:
- Impl version (if relevant):
```

- [ ] **Step 2:** Create `.github/ISSUE_TEMPLATE/feature.md`:

```markdown
---
name: Feature request
about: Propose a new feature
labels: enhancement
---

**Problem this solves:**

**Proposed change:**

**Impl(s) affected:** <!-- typescript / csharp / python / spec / all -->

**Does this require a spec change?** <!-- yes / no -->

**Conformance impact:** <!-- new scenarios needed? expected-output changes? -->
```

- [ ] **Step 3:** Create `.github/ISSUE_TEMPLATE/conformance-divergence.md`:

```markdown
---
name: Conformance divergence
about: Two or more impls disagree on a conformance scenario
labels: conformance
---

**Scenario file:** <!-- e.g., spec/conformance/scenarios/sas.json -->

**Expected output file:** <!-- e.g., spec/conformance/expected/sas.candidates.json -->

**Impls that diverge:**

- [ ] typescript
- [ ] csharp
- [ ] python

**Observed values per impl:**

```
typescript: ...
csharp: ...
python: ...
```

**Suspected cause:** <!-- spec ambiguity? impl bug? numerical drift? -->
```

- [ ] **Step 4:** Create `.github/ISSUE_TEMPLATE/question.md`:

```markdown
---
name: Question
about: Ask about usage, design, or contributions
labels: question
---

**Your question:**

**What you've already tried or read:**
```

- [ ] **Step 5:** Create `.github/PULL_REQUEST_TEMPLATE.md`:

```markdown
## Summary

<!-- 1-3 bullets describing the change -->

## Impls touched

- [ ] spec
- [ ] typescript
- [ ] csharp
- [ ] python
- [ ] tools / CI / docs

## Conformance

- [ ] No conformance impact
- [ ] New conformance scenarios added under `spec/conformance/`
- [ ] Existing expected outputs updated (explain why)
- [ ] All three impls pass conformance locally

## Checklist

- [ ] Conventional commit messages
- [ ] Tests added / updated
- [ ] Lint and format checks pass per impl
- [ ] If spec changed: ADR added or amended under `spec/ADRs/`
```

- [ ] **Step 6:** Create `.github/dependabot.yml`:

```yaml
version: 2
updates:
  - package-ecosystem: npm
    directory: "/langs/typescript"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
    commit-message: { prefix: "chore(ts)" }

  - package-ecosystem: nuget
    directory: "/langs/csharp"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
    commit-message: { prefix: "chore(cs)" }

  - package-ecosystem: pip
    directory: "/langs/python"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
    commit-message: { prefix: "chore(py)" }

  - package-ecosystem: github-actions
    directory: "/"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
    commit-message: { prefix: "chore(ci)" }

  - package-ecosystem: cargo
    directory: "/langs/typescript/src-tauri"
    schedule: { interval: weekly }
    open-pull-requests-limit: 5
    commit-message: { prefix: "chore(tauri)" }
```

- [ ] **Step 7:** Commit:

```bash
git add .github/ISSUE_TEMPLATE/ .github/PULL_REQUEST_TEMPLATE.md .github/dependabot.yml
git commit -m "chore(gh): add issue + PR templates and dependabot config"
```

---

## Phase J — Create remote and push

### Task 24: Create GitHub repo and push

**Files:** none new

- [ ] **Step 1:** Verify `gh` CLI is authenticated as `thekaveh`:

```bash
gh auth status | grep -E "thekaveh" || gh auth login
```

- [ ] **Step 2:** Create the public remote repo without an initial commit:

```bash
gh repo create thekaveh/GuideArch \
  --public \
  --description "Modern fuzzy multi-criteria decision analysis for software architecture. TypeScript+Tauri+Svelte, C#+Avalonia, and Python+NiceGUI implementations sharing one spec, all built on VMx." \
  --homepage "https://github.com/thekaveh/GuideArch"
```

- [ ] **Step 3:** Add the remote and push:

```bash
git remote add origin https://github.com/thekaveh/GuideArch.git
git branch -M main
git push -u origin main
```

- [ ] **Step 4:** Push the submodule registration (the submodule pointer is already in the commits):

```bash
git submodule sync --recursive
```

- [ ] **Step 5:** Verify in the browser: open `https://github.com/thekaveh/GuideArch` and confirm the README, license, and submodule pointer are visible.

- [ ] **Step 6:** Set repo topics:

```bash
gh repo edit thekaveh/GuideArch \
  --add-topic mvvm \
  --add-topic topsis \
  --add-topic fuzzy-logic \
  --add-topic decision-analysis \
  --add-topic software-architecture \
  --add-topic avalonia \
  --add-topic tauri \
  --add-topic svelte \
  --add-topic nicegui \
  --add-topic vmx
```

---

### Task 25: Verify all CI workflows succeed on the remote

**Files:** none

- [ ] **Step 1:** Wait for the initial CI run and check status:

```bash
sleep 60
gh run list --limit 10
```

- [ ] **Step 2:** Watch the most recent run interactively, or poll until complete:

```bash
gh run watch
```

Expected: `spec`, `typescript`, `csharp`, `python` all succeed. `conformance` exits 0 (stub). `vmx-bump` is scheduled only — skip.

- [ ] **Step 3:** If any workflow fails, capture the log and fix:

```bash
gh run view --log-failed
```

Fix the underlying cause in a new commit (e.g., missing prereq in CI, path typo). Do NOT mask failures by removing checks.

---

### Task 26: Configure branch protection

**Files:** none

- [ ] **Step 1:** Require status checks + PRs to merge to `main`:

```bash
gh api -X PUT repos/thekaveh/GuideArch/branches/main/protection \
  -H "Accept: application/vnd.github+json" \
  -f required_status_checks[strict]=true \
  -f 'required_status_checks[contexts][]=spec' \
  -f 'required_status_checks[contexts][]=typescript' \
  -f 'required_status_checks[contexts][]=csharp' \
  -f 'required_status_checks[contexts][]=python' \
  -f 'required_status_checks[contexts][]=conformance' \
  -F enforce_admins=false \
  -f required_pull_request_reviews[required_approving_review_count]=0 \
  -f required_pull_request_reviews[dismiss_stale_reviews]=true \
  -F allow_force_pushes=false \
  -F allow_deletions=false
```

If running solo and no reviewer is available, `required_approving_review_count=0` keeps the gate light while still enforcing CI checks.

- [ ] **Step 2:** Verify:

```bash
gh api repos/thekaveh/GuideArch/branches/main/protection | jq .required_status_checks.contexts
```

Expected: an array containing `spec`, `typescript`, `csharp`, `python`, `conformance`.

---

### Task 27: Tag `v0.0.0-bootstrap`

**Files:** none

- [ ] **Step 1:** Tag and push:

```bash
git tag -a v0.0.0-bootstrap -m "M0 — repo bootstrap complete; three impls hello-VMx, CI green"
git push origin v0.0.0-bootstrap
```

- [ ] **Step 2:** Create a GitHub release:

```bash
gh release create v0.0.0-bootstrap \
  --title "v0.0.0-bootstrap — M0" \
  --notes "M0 milestone complete. Repo scaffolded, VMx vendored as submodule (commit e2b23f8), three impls (TS+Tauri+Svelte / C#+Avalonia / Python+NiceGUI) running hello-VMx, CI green on all matrices. Not yet usable as an application. Next: M1 — formalize the spec and port TOPSIS in all three impls."
```

- [ ] **Step 3:** Verify in the browser at `https://github.com/thekaveh/GuideArch/releases`.

---

## Plan self-review

**Spec coverage** — checked against `docs/design/2026-05-29-guidearch-rewrite-design.md`:

- §3 Architecture (three impls + spec + VMx submodule): covered by Tasks 11, 13, 15–17, 18.
- §4 Repo Layout: covered by Tasks 1–12 (everything outside `langs/`), Tasks 13–18 (per-impl scaffolds), Tasks 19–23 (`.github/` scaffolding).
- §5 Domain Model: deferred to M1 plan (M0 contains a `HelloVmx` placeholder per impl, not the real Models).
- §6 Scenario file format, §7 Algorithms, §8 ViewModel layer, §10 Conformance: all deferred to M1 plan.
- §9 Per-Impl: scaffold portions covered by Tasks 13–18; full bindings + UI deferred to M2+.
- §11 GitHub plan: covered by Tasks 4 (license), 9–10 (ADRs), 11 (submodule pin), 19–23 (CI + governance), 24–27 (push + release).
- §13 M0 milestone definition ("Repo at thekaveh/GuideArch, VMx submodule wired, three lang scaffolds, CI green on empty workloads, license + governance files"): every clause is realized by a numbered task.

**Placeholder scan:** no "TBD" / "TODO" inside steps. Each step has a concrete command or content. Task 11 has a documented fallback path if the `v2.1.0` tag is missing; this is a real condition handler, not a placeholder.

**Type consistency:** the `HelloVmx` API is used identically across all three impls (`HelloVmx.Run()` C#, `helloVMx()` TS, `hello_vmx()` Python). Each instantiates a `ComponentVM<Greeting>`, constructs it, returns a descriptive string. VMx-prefixed types (`ComponentVM`, `construct`/`Construct`/`construct()`) are spelled consistently per language idiom (PascalCase C#, camelCase TS, snake_case Python).

**Hard rules:** every `git commit` step uses a plain Conventional Commit message; none contain `Co-Authored-By`, "Claude", "AI", or "Generated with". `.gitignore` explicitly excludes `.claude/`, `.superpowers/`, and other AI tool artifact directories (Task 2).

**Scope:** focused on M0 only; M1–M5 are explicitly out of scope. Each task is self-contained and committable.
