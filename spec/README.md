# GuideArch Spec

This directory is the language-neutral source of truth for GuideArch. Every implementation must conform to it.

## Layout

- **[`domain/`](domain/)** — domain model and load-time contract.
  - [`scenario.schema.json`](domain/scenario.schema.json) — JSON Schema 2020-12 for the input format.
  - [`glossary.md`](domain/glossary.md) — vocabulary.
  - [`invariants.md`](domain/invariants.md) — load-time validation rules.
- **[`algorithms/`](algorithms/)** — TOPSIS pipeline and reference cards.
  - [`topsis.md`](algorithms/topsis.md) — the canonical TOPSIS pipeline with magic-number table and tie-break rule.
  - [`critical-decisions.md`](algorithms/critical-decisions.md), [`critical-constraints.md`](algorithms/critical-constraints.md) — reference cards.
- **[`viewmodels.md`](viewmodels.md)** — shared ViewModel tree shape: command names, observable property names, dirty-tracking, and the re-solve trigger lists every impl mirrors.
- **[`editors.md`](editors.md)** — editor semantics: cascade rules for Delete, add-with-defaults behavior, validation timing.
- **[`charts.md`](charts.md)** — chart contracts: fuzzy-decomposition triangle layout, axis/series conventions, color tokens.
- **[`design-system.md`](design-system.md)** — the visual language all three impls render against (color tokens, type scale, spacing, component specs).
- **[`release.md`](release.md)** — release process, versioning policy, and the monorepo tag scheme.
- **[`conformance/`](conformance/)** — cross-impl numeric conformance corpus.
  - [`scenarios/sas.json`](conformance/scenarios/sas.json), [`scenarios/eds.json`](conformance/scenarios/eds.json) — seed scenarios imported from the legacy XML via [`tools/import-legacy-xml.py`](../tools/import-legacy-xml.py).
  - `expected/*.json` — reference outputs (candidates, critical decisions, critical constraints) produced by the Python reference implementation and matched by the C# and TypeScript impls within [`tolerances.json`](conformance/tolerances.json).
  - [`tolerances.json`](conformance/tolerances.json) — `1e-9` absolute on scalar outputs; ranking exact under the spec tie-break rule (`alternativeIds` lexicographic).
- **[`ADRs/`](ADRs/)** — architecture decision records:
  - [ADR-0001 — Three implementations sharing one spec; VMx as submodule](ADRs/0001-three-impls-vmx-submodule.md)
  - [ADR-0002 — JSON Schema for scenario files (not legacy XML)](ADRs/0002-json-schema-not-xml.md)
  - [ADR-0003 — TOPSIS as in-repo code; no Microsoft Solver Foundation](ADRs/0003-topsis-no-msf.md)
  - [ADR-0004 — MIT License](ADRs/0004-mit-license.md)
  - [ADR-0005 — Single monorepo version; all three impls release together](ADRs/0005-single-monorepo-version.md)
  - [ADR-0006 — NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)](ADRs/0006-nicegui-over-shiny.md)

## Conformance contract

Each implementation ships a conformance runner that reads every file in `conformance/scenarios/`, runs that impl's TOPSIS + critical-decisions + critical-constraints, and asserts the outputs match `conformance/expected/` within `conformance/tolerances.json`. CI (`.github/workflows/conformance.yml`) runs all three runners on every PR and push; divergence fails the build.

To run the runners locally:

```bash
# Python
cd langs/python && uv run python -m guidearch.conformance.runner

# C#
cd langs/csharp && dotnet build && dotnet run --project src/GuideArch.Conformance

# TypeScript
cd langs/typescript && pnpm conformance
```

Each prints `PASS` or a structured diff identifying which scenario, which field, what value vs. expected.
