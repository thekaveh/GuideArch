# Domain glossary

Terms used in `scenario.schema.json` and across the algorithms.

| Term | Definition |
|---|---|
| **Decision** | A choice point the architect needs to resolve (e.g. "Database choice"). Owns a unique `id` and a display `name`. |
| **Alternative** | One concrete option for a single decision (e.g. "PostgreSQL" for "Database choice"). Belongs to exactly one decision via `decisionId`. |
| **Property** | A quality attribute used to score candidates (e.g. "Cost", "Reliability"). Has a `kind ∈ {min, max}` (whether lower or higher is better) and a positive `weight` (called *priority* in the legacy code). |
| **TriangularFuzzy** | A triangular fuzzy number `(lower, modal, upper)`. Used to represent uncertainty in coefficient estimates. Implementations SHOULD verify `lower ≤ modal ≤ upper` at scenario load time, but the legacy did not, so this is a warning, not a fatal error. |
| **NormalizedFuzzy** | The post-PIS/NIS-normalization triple `(positive, average, negative)` in `[0, 1]³`. Computed by the TOPSIS pipeline; not present in input scenarios. |
| **Coefficient** | The `(alternativeId, propertyId, value)` mapping that says "this alternative scores this fuzzy value on this property." A scenario must provide a coefficient for every `(alternative, property)` pair; missing coefficients are a validation error. |
| **Constraint** | A tagged-union rule with three kinds. `threshold` says a property's aggregate over a candidate must lie in `[min, max]`. `dependency` says if `source` is in a candidate then `target` must be too (biconditional; see `spec/algorithms/topsis.md` §3.2 step 1 for the canonical formulation). `conflict` says `A` and `B` cannot both be in a candidate. |
| **Candidate** | A complete architecture — exactly one alternative per decision. The Cartesian product over decisions gives all *raw* candidates; constraints filter that set down to *feasible* candidates. |
| **Score** | TOPSIS output for a candidate. Scalar in `[0, 1]`. **Lower is better.** Output of `score(c)` in `topsis.md` §3.10. |
| **Rank** | 0-based position after sorting candidates by score ascending. Ties are broken by lexicographic order on `alternativeIds`. |
| **Critical decision** | A decision whose alternatives most strongly drive the top-N candidates' scores. Output of `criticalDecisions(scenario)`. Lower critical-score = more critical. |
| **Critical constraint** | A constraint that eliminates the most candidates when applied to the unconstrained candidate set. Constraints with elimination count 0 are flagged "redundant." |
| **Aggregation mode** | `config.aggregation`. Either `sum` (φ⁺+φᵃ+φ⁻) or `max` (max of the three). Default `max`. Equivalent to the legacy `PhiCalculationApproach` enum. |
| **Weights** | `config.weights = {positive, average, negative}`. Must sum to 1. Equivalent to the legacy `(Wp, Wa, Wn)` triple. |
| **PIS** | Positive Ideal Solution. Per-Z-dimension extremes that represent "as good as it gets" on each axis. See `topsis.md` §3.7. |
| **NIS** | Negative Ideal Solution. Per-Z-dimension extremes representing "worst on this axis." See `topsis.md` §3.7. |
| **φ⁺, φᵃ, φ⁻** | Per-dimension weighted closeness; the three components that aggregate to a single `score`. |
