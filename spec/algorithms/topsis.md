# TOPSIS algorithm — formal specification

**Status:** Authoritative. All three impls must conform.

This document specifies the **exact** TOPSIS variant GuideArch uses. It is the result of reverse-engineering the legacy `.Old/GuideArch.Model/Space.cs` so that the rewritten implementations preserve numerical behavior. Where the legacy algorithm differs from textbook TOPSIS, the legacy semantics win — they are what the existing scenarios (`SAS.xml`, `EDS.xml`) were authored against.

## 1. Inputs

A `ScenarioM` provides:

- `decisions`: list of decisions `D = {d₁, d₂, …, d_m}`
- `alternatives`: `A = {a_{ij}}` where `a_{ij}` is the `j`-th alternative of decision `dᵢ`
- `properties`: list `P = {p₁, p₂, …, p_n}` with each `pₖ` carrying `kind ∈ {min, max}` and `weight ∈ ℝ⁺` (called `Priority` in the legacy code)
- `coefficients`: a map `(alternativeId, propertyId) → TriangularFuzzy(lower, modal, upper)`
- `constraints`: tagged unions of three kinds (`threshold` / `dependency` / `conflict`)
- `config`:
  - `aggregation ∈ {sum, max}` (`Sum` or `Max` in the legacy `PhiCalculationApproach` enum). **Default: `max`.**
  - `weights = (w⁺, wᵃ, w⁻) ∈ ℝ³⁺` satisfying `w⁺ + wᵃ + w⁻ = 1`. Presets from the legacy `SolutionApproach` enum:
    - `Risky`: `(1/2, 1/4, 1/4)`
    - `Normal`: `(1/3, 1/3, 1/3)` (default)
    - `Traditional`: `(0, 1, 0)`
    - `Conservative`: `(1/4, 1/4, 1/2)`
    - `Custom`: user-supplied (must still sum to 1)

`TriangularFuzzy` is `{ lower ∈ ℝ, modal ∈ ℝ, upper ∈ ℝ }`. The constructor does **not** enforce `lower ≤ modal ≤ upper`; legacy code assumes it.

## 2. Outputs

`solve(scenario) → RankedCandidates`:

- An ordered list of `CandidateM`, each with:
  - `alternativeIds`: one alternative per decision
  - `triangularValue`: aggregated `TriangularFuzzy` over all properties (pre-normalization)
  - `normalizedValue`: a `NormalizedFuzzy` (`{positive, average, negative}`) in `[0, 1]³`
  - `score`: scalar in `[0, 1]` — **lower is better** (matches legacy ascending sort)
  - `rank`: 0-based position after sorting

## 3. Pipeline

### 3.1 Enumerate raw candidates

`C₀ = ∏ᵢ alternatives(dᵢ)` — Cartesian product, one alternative per decision.

### 3.2 Filter — in this order

For each `c ∈ C₀`, retain `c` only if it satisfies all three constraint kinds, evaluated in this exact order:

1. **Dependency constraints**: for each `(sourceAlternativeId → targetAlternativeId)`, keep `c` iff
   `(source ∉ c ∧ target ∉ c) ∨ (source ∈ c ∧ target ∈ c)`.

   > *Note:* The legacy code on `Space.cs` line 975 reads `dc.Dependee` twice — a literal transcription of the buggy form would always evaluate true. The intended semantics, faithful to the rest of the codebase and to the architectural meaning of "dependency" (target depends on source), are the biconditional above. **The spec mandates the biconditional; impls are not bug-compatible with the literal source.**

2. **Conflict constraints**: for each `(alternativeAId, alternativeBId)`, keep `c` iff `¬(A ∈ c ∧ B ∈ c)`.

3. **Threshold constraints**: for each `(propertyId, threshold)`, compute
   `contribution = ⊕ over a ∈ c : coefficient(a, propertyId)`
   where `⊕` is componentwise triangular addition (§4.1). Defuzzify via the **lower vertex only** — `defuzz(t) = t.lower`. Then:
   - If `kind(property) == min`: keep iff `contribution.lower ≤ threshold`.
   - If `kind(property) == max`: keep iff `contribution.lower ≥ threshold`.

   Let `C` be the candidates that survive all three filters.

### 3.3 Aggregate fuzzy value per (candidate, property)

For each `c ∈ C` and each property `pₖ`:

```
contribution(c, pₖ) = ⊕ over a ∈ c : coefficient(a, pₖ)
```

(Componentwise triangular sum — see §4.1.)

### 3.4 Per-property normalizer

For each `pₖ`, compute a scalar divisor `M(pₖ)` over **the original alternative pool** (not the filtered `C`):

```
M(pₖ) = Σᵢ  ( if kind(pₖ) == max
              then max_{j} coefficient(a_{ij}, pₖ).upper
              else max_{j} coefficient(a_{ij}, pₖ).lower )
```

I.e., per decision group, pick the alternative whose vertex (upper if maximizing, lower if minimizing) is largest in magnitude; sum those vertex values across decisions. This is the legacy `maxOfProperty()` function (`Space.cs` 936–945).

### 3.5 Total triangular value

For each candidate `c`, compute a single `TriangularFuzzy`:

```
totalValue(c) = ⊕ over pₖ : sign(pₖ) · weight(pₖ) · contribution(c, pₖ) ⊘ M(pₖ)
```

where:
- `sign(pₖ) = +1` if `kind(pₖ) == min`, `−1` if `kind(pₖ) == max`
- `weight(pₖ)` is the property's `Priority`
- `⊘` is scalar division (§4.1)
- `⊕` is componentwise triangular addition (negative scaling flips the sign of each vertex; ordering may invert post-scale)

The result is candidate `c`'s `triangularValue`.

### 3.6 Convert to Z-space

For any `TriangularFuzzy t`:

```
toZ(t) = { average: t.modal,
           positive: |t.modal − t.lower|,
           negative: |t.upper − t.modal| }
```

Apply to every `totalValue(c)`:

```
z(c) = toZ(totalValue(c))
```

### 3.7 PIS / NIS per Z-dimension

Over the set `{ z(c) : c ∈ C }`:

```
PIS = { average:  min_{c} z(c).average,
        positive: max_{c} z(c).positive,
        negative: min_{c} z(c).negative }

NIS = { average:  max_{c} z(c).average,
        positive: min_{c} z(c).positive,
        negative: max_{c} z(c).negative }
```

Note this is **dimension-wise extremes in Z-space**, not the dimension-wise extremes in TriangularFuzzy-space, and the convention does **not** consult `Property.kind` at this step (the sign was already absorbed into the totalValue via §3.5).

### 3.8 Normalize each candidate

For each `c`, with `z = z(c)`, compute `n(c) = NormalizedFuzzy{positive, average, negative} ∈ [0, 1]³`:

```
n.average  = clip01( (z.average  − PIS.average ) / (NIS.average  − PIS.average ) )
n.positive = clip01( (PIS.positive − z.positive) / (PIS.positive − NIS.positive) )
n.negative = clip01( (z.negative − PIS.negative) / (NIS.negative − PIS.negative) )
```

where `clip01(x) = max(0, min(1, x))`. The denominator may be `0` (degenerate case — all candidates have the same value on this dimension); when `denominator == 0`, define `n = 0`. Lower is better on all three dimensions (closer to PIS).

### 3.9 Per-dimension weighted closeness

```
φ⁺(c) = w⁺ · n.positive
φᵃ(c) = wᵃ · n.average
φ⁻(c) = w⁻ · n.negative
```

### 3.10 Final score and rank

```
score(c) = if config.aggregation == sum then φ⁺(c) + φᵃ(c) + φ⁻(c)
                                        else max(φ⁺(c), φᵃ(c), φ⁻(c))
```

Sort `C` by `score` ascending — **lower score = better**. Assign `rank` from `0`.

**Tie-breaking:** the legacy uses LINQ stable sort, retaining Cartesian product enumeration order. The spec mandates **deterministic tie-breaking by `alternativeIds` lexicographic order** (the legacy's order is enumeration-dependent and not portable across languages).

## 4. Operations on `TriangularFuzzy`

### 4.1 Arithmetic

For `s ∈ ℝ` and triangular `a = (aL, aM, aU)`, `b = (bL, bM, bU)`:

```
a ⊕ b  =  (aL + bL,  aM + bM,  aU + bU)
a ⊖ b  =  (aL − bL,  aM − bM,  aU − bU)
s ⊙ a  =  (s · aL,   s · aM,   s · aU)
a ⊘ s  =  (aL / s,   aM / s,   aU / s)              for s ≠ 0
```

Scalar multiplication by a negative number flips the sign of every vertex; the ordering relation `lower ≤ modal ≤ upper` will be reversed but downstream code never assumes it holds after totalValue.

### 4.2 Defuzzification (used only by §3.2 step 3)

```
defuzz_lower(t) = t.lower
```

Yes — only the lower vertex, per the legacy. No centroid, no midpoint.

## 5. Critical decisions

A sensitivity-style ranking of decisions by how strongly the top candidates' choices in that decision drive their score.

Let `R = sort(C, by score asc)`. Take `Rₜ = R[0 : min(20, |R|)]` — the legacy hard-codes 20.

For each decision `dᵢ`:

```
contribution(dᵢ) = ⊕ over c ∈ Rₜ :
                     exp(−0.1 · rank(c)) ·
                     contributionOfAlternative(c.alternativeIds[i])
```

where

```
contributionOfAlternative(a) = ⊕ over pₖ :
                                sign(pₖ) · weight(pₖ) · coefficient(a, pₖ) ⊘ M(pₖ)
```

(this is the same per-property normalization as §3.5, but for a single alternative not a full candidate).

Then convert `contribution(dᵢ)` to Z-space (§3.6), compute PIS/NIS over `{ z(dᵢ) : dᵢ ∈ D }` (§3.7), normalize (§3.8), and aggregate with `aggregation = max` (the legacy hardcodes `Max` here, ignoring `config.aggregation`).

Output: decisions sorted by criticality score ascending — **lower score = more critical**.

## 6. Critical constraints

For each constraint `k` in the scenario:

1. Build a clone of the scenario with **no constraints**.
2. Generate `C_unconstrained = ∏ᵢ alternatives(dᵢ)`.
3. Apply only `k` and count eliminated candidates: `eliminated(k) = |C_unconstrained| − |applied_k(C_unconstrained)|`.

Output: constraints sorted by `eliminated` **descending** — most-binding first. Constraints with `eliminated == 0` are flagged "redundant" by the UI (spec/charts.md §4).

## 7. Conformance gates

Two-level tolerance:

- **Per-impl numerical tolerance**: each impl's scalar outputs must agree across runs to `1e-9` absolute.
- **Cross-impl conformance vs spec corpus**: outputs must agree with `spec/conformance/expected/*.json` to the tolerances specified in `spec/conformance/tolerances.json`.
- **Cross-impl conformance vs legacy baseline** (initial seed): `1e-6` absolute on scalar score, **stable ranking under spec tie-break rule**. Legacy ties are enumeration-dependent; the spec mandates the lexicographic tie-break (§3.10) so cross-impl rankings agree exactly.

## 8. Magic numbers — all in one place

| Constant | Value | Where used | Origin |
|---|---|---|---|
| Top-N for critical decisions | 20 | §5 | legacy `Space.cs:244` |
| Decay coefficient | 0.1 | §5 (`exp(−0.1·rank)`) | legacy `Space.cs:250` |
| Default `aggregation` | `max` | §3.10, §1 | legacy `Space.cs:49` |
| Default weights | `Normal = (1/3, 1/3, 1/3)` | §1 | legacy `Space.cs` Z presets |
| Defuzz method (thresholds) | lower vertex | §3.2 step 3, §4.2 | legacy `Space.cs:968` |
| Tolerance per-impl | `1e-9` | §7 | spec choice |
| Tolerance vs legacy seed | `1e-6` | §7 | spec choice |

## 9. Non-conformances with textbook TOPSIS

1. **Per-property normalization uses `Property.kind` to choose vertex** (upper for max, lower for min) rather than the standard `√(Σ x²)` denominator.
2. **PIS/NIS computed in Z-space** rather than over the criterion matrix directly.
3. **Three weighted dimensions (φ⁺, φᵃ, φ⁻) aggregated with `sum` or `max`** rather than the standard `d⁻ / (d⁺ + d⁻)` closeness coefficient.
4. **Threshold defuzz uses lower vertex only**, not centroid.
5. **Lower score = better** (matches Z normalization where 0 = at PIS).
