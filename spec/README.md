# GuideArch Spec

This directory is the language-neutral source of truth for GuideArch. Every implementation must conform to it.

## Layout

- **`domain/`** — populated at M1.
  - `scenario.schema.json` — JSON Schema 2020-12 for the input format.
  - `glossary.md` — vocabulary.
  - `invariants.md` — load-time validation rules.
- **`algorithms/`** — populated at M1.
  - `topsis.md` — the canonical TOPSIS pipeline with magic-number table and tie-break rule.
  - `critical-decisions.md`, `critical-constraints.md` — reference cards.
- `viewmodels.md` — shared ViewModel tree shape (names, commands, observable properties). **Authored during M2.**
- **`conformance/`** — populated at M1.
  - `scenarios/sas.json`, `scenarios/eds.json` — seed scenarios imported from the legacy XML via `tools/import-legacy-xml.py`.
  - `expected/*.json` — reference outputs (candidates, critical decisions, critical constraints) produced by the Python reference implementation and matched by the C# and TypeScript impls within `tolerances.json`.
  - `tolerances.json` — `1e-9` absolute on scalar outputs; ranking exact under the spec tie-break rule (`alternativeIds` lexicographic).
- `ADRs/` — architecture decision records.

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
