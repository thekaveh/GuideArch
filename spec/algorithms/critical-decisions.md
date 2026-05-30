# Critical decisions — formal specification

**Status:** Authoritative. See [`topsis.md`](topsis.md) §5 for the canonical algorithm — this document is the reference card.

## Summary

`criticalDecisions(scenario) → RankedDecisions` measures, for each decision, how strongly the alternatives chosen by the top-ranked candidates drive their score.

## Inputs

- A `ScenarioM` (same shape as for `solve`).
- The output of `solve(scenario)` — specifically the ranked candidate list. (Implementations may call `solve` internally rather than require the caller to pass results in.)

## Outputs

A list of `CriticalDecisionM`, sorted by `score` ascending (lower = more critical), with fields:

- `decisionId`
- `triangularValue`: aggregate `TriangularFuzzy` (pre-normalization)
- `normalizedValue`: `NormalizedFuzzy`
- `score`: scalar in `[0, 1]`
- `rank`: 0-based

## Algorithm

See `topsis.md` §5. Salient parameters:

- **Top-N = `min(20, |candidates|)`** — hardcoded by the legacy.
- **Decay coefficient = `0.1`** — used in `exp(−0.1 · rank)` weighting.
- **Aggregation mode = `max`** — fixed, ignores `config.aggregation`.
- **PIS / NIS / weights / normalization** — same as `topsis.md` §3.6–3.10, but the candidate set is replaced with the decision set in Z-space.

## Edge cases

- `|candidates| == 0`: returns an empty list.
- `|candidates| == 1`: each decision's contribution comes from a single weighted alternative; PIS == NIS for all decisions ⇒ all normalized values are `0` ⇒ all scores are `0` ⇒ all decisions tied. Impls return decisions in `scenario.decisions` order.
