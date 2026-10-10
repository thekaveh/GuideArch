# GuideArch documentation reference

Read most-to-least essential. Each link includes when to use it.

## 1 Specification & conformance

- **[`spec/`](../spec/README.md)** — the language-neutral source of truth that every implementation must satisfy. Contents:
  - **[`spec/algorithms/topsis.md`](../spec/algorithms/topsis.md)** — the canonical TOPSIS pipeline with magic-number table and tie-break rule. *(M1)*
  - **[`spec/algorithms/critical-decisions.md`](../spec/algorithms/critical-decisions.md)**, **[`spec/algorithms/critical-constraints.md`](../spec/algorithms/critical-constraints.md)** — reference cards. *(M1)*
  - **[`spec/domain/scenario.schema.json`](../spec/domain/scenario.schema.json)** — JSON Schema 2020-12 for the input format. *(M1)*
  - **[`spec/domain/glossary.md`](../spec/domain/glossary.md)**, **[`spec/domain/invariants.md`](../spec/domain/invariants.md)** — vocabulary and load-time validation rules. *(M1)*
  - **[`spec/viewmodels.md`](../spec/viewmodels.md)** — shared ViewModel tree shape: command names, observable property names, dirty-tracking, and re-solve trigger lists every impl mirrors. *(M2)*
  - **[`spec/editors.md`](../spec/editors.md)** — editor semantics: cascade rules for Delete (decision → alternatives → coefficients → constraints), add-with-defaults behavior, validation timing. *(M3)*
  - **[`spec/charts.md`](../spec/charts.md)** — chart contracts: fuzzy-decomposition triangle layout, axis/series conventions, color tokens. *(M4)*
  - **[`spec/design-system.md`](../spec/design-system.md)** — the visual language (color tokens, type scale, spacing, component specs) that all three impls render against. *(v1.0)*
  - **[`spec/release.md`](../spec/release.md)** — release process, versioning policy, monorepo tag scheme. *(v1.0)*
  - **[`spec/conformance/`](../spec/conformance/)** — the seed corpus: `scenarios/sas.json` (10 decisions, 25 alternatives, 7 properties), `scenarios/eds.json` (same shape), and the matching `expected/*.json` outputs the three impls must reproduce within `tolerances.json` (1e-9 absolute on scalars; ranking exact). *(M1, expanded M2-M4)*

## 2 Architecture decision records

Numbered rationale for each non-obvious design choice. Read when questioning *why* something is the way it is.

- [ADR-0001 — Three implementations sharing one spec; VMx as submodule](../spec/ADRs/0001-three-impls-vmx-submodule.md)
- [ADR-0002 — JSON Schema for scenario files (not legacy XML)](../spec/ADRs/0002-json-schema-not-xml.md)
- [ADR-0003 — TOPSIS as in-repo code; no Microsoft Solver Foundation](../spec/ADRs/0003-topsis-no-msf.md)
- [ADR-0004 — MIT License](../spec/ADRs/0004-mit-license.md)
- [ADR-0005 — Single monorepo version; all three impls release together](../spec/ADRs/0005-single-monorepo-version.md)
- [ADR-0006 — NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)](../spec/ADRs/0006-nicegui-over-shiny.md)

## 3 Release history & governance

- **[CHANGELOG.md](../CHANGELOG.md)** — Keep-a-Changelog–style development and release history plus the running `[Unreleased]` section for development changes. *Start here when answering "what shipped?" — more reliable than scanning `git log`.*
- **[CONTRIBUTING.md](../CONTRIBUTING.md)** — feature workflow (spec-first, all three impls in lockstep), local development, test layout, code style per language. *Read before opening a PR.*
- **[CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md)** — Contributor Covenant 2.1. Report violations to kaveh.razavi@gmail.com.
- **[SECURITY.md](../SECURITY.md)** — private vulnerability reporting.

## 4 Maintenance & audit artifacts

- **[docs/contract-ledger.md](../docs/contract-ledger.md)** — consumed external-contract ledger for VMx, framework packages, Docker images, CI actions, and invoked toolchains. Update it when a lockfile, submodule pin, Docker digest, or workflow action changes.
- **[VMx 3.1 refactor audit report](../docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md)** — summary of the VMx 3.1 upgrade/refactor investigation across Python, TypeScript, and C#.
- **[VMx 3.1 audit design](../docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-design.md)** and **[execution plan](../docs/superpowers/plans/2026-07-02-vmx-3-1-refactor-audit.md)** — planning context for the VMx audit branch.
- **[VMx capability notes](../docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md)**, **[current usage](../docs/superpowers/specs/vmx-3-1-audit/current-usage.md)**, **[replacement ledger](../docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md)**, **[test impact](../docs/superpowers/specs/vmx-3-1-audit/test-impact.md)**, **[baseline](../docs/superpowers/specs/vmx-3-1-audit/baseline.md)**, and **[LOC baseline](../docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt)** — detailed evidence behind the VMx audit report.

