# GuideArch Spec

This directory is the language-neutral source of truth for GuideArch. Every implementation must conform to it.

## Layout

- `domain/` — scenario JSON schema, glossary, invariants. **Authored during M1.**
- `algorithms/` — formal write-ups of TOPSIS, critical-decisions, critical-constraints. **Authored during M1.**
- `viewmodels.md` — shared ViewModel tree shape (names, commands, observable properties). **Authored during M2.**
- `conformance/` — input scenarios + expected outputs + numerical tolerances.
- `ADRs/` — architecture decision records.

## Conformance contract

Each implementation under `langs/<impl>/tests/conformance/` reads every file in `conformance/scenarios/`, runs the impl's TOPSIS + analyses, and asserts that outputs match `conformance/expected/` within `conformance/tolerances.json`. CI fails any divergence.
