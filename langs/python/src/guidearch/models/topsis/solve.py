"""TOPSIS solver — implements topsis.md §3 step-by-step.

Every numbered comment below maps to the corresponding §3.x step.
"""

from __future__ import annotations

import itertools
import warnings as _warnings
from typing import TYPE_CHECKING

from guidearch.models.candidate import CandidateM
from guidearch.models.constraint import (
    ConflictConstraint,
    DependencyConstraint,
    ThresholdConstraint,
)
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM

if TYPE_CHECKING:
    from guidearch.models.scenario import ScenarioM


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------


def _clip01(x: float) -> float:
    return max(0.0, min(1.0, x))


def _to_z(t: TriangularFuzzyM) -> NormalizedFuzzyM:
    """Convert TriangularFuzzy to Z-space — topsis.md §3.6."""
    return NormalizedFuzzyM(
        positive=abs(t.modal - t.lower),
        average=t.modal,
        negative=abs(t.upper - t.modal),
    )


def _compute_normalizer(
    scenario: ScenarioM,
) -> dict[str, float]:
    """Per-property normalizer M(pₖ) — topsis.md §3.4.

    Uses the original alternative pool (not filtered C).
    For 'max' properties: max over each decision group of coefficient.upper.
    For 'min' properties: max over each decision group of coefficient.lower.
    Then sums those per-group maxima across decisions.
    """
    # Build coeff lookup: (alt_id, prop_id) -> TriangularFuzzyM
    coeff: dict[tuple[str, str], TriangularFuzzyM] = {
        (c.alternative_id, c.property_id): c.value for c in scenario.coefficients
    }
    # Group alternatives by decision
    alts_by_dec: dict[str, list[str]] = {}
    for a in scenario.alternatives:
        alts_by_dec.setdefault(a.decision_id, []).append(a.id)

    M: dict[str, float] = {}
    for p in scenario.properties:
        total = 0.0
        for d in scenario.decisions:
            group_alts = alts_by_dec.get(d.id, [])
            if not group_alts:
                continue
            if p.kind == "max":
                best = max(coeff[(a_id, p.id)].upper for a_id in group_alts)
            else:
                best = max(coeff[(a_id, p.id)].lower for a_id in group_alts)
            total += best
        M[p.id] = total
    return M


def _warn_degenerate_normalizers(M: dict[str, float]) -> None:
    """Invariant 10.1: emit a warning for every degenerate property (M=0).

    Called once at the top of solve(); _alt_contribution then silently skips.
    critical_decisions() recomputes M for its decision-set normalization but
    does NOT call this helper, so the warning fires once per VM solve call
    instead of twice. See topsis.md §3.4.
    """
    for p_id, total in M.items():
        if total == 0.0:
            _warnings.warn(
                f"Property '{p_id}' has M=0; skipping to avoid division by zero",
                stacklevel=3,
            )


def _alt_contribution(
    alt_id: str,
    scenario: ScenarioM,
    coeff: dict[tuple[str, str], TriangularFuzzyM],
    M: dict[str, float],
) -> TriangularFuzzyM:
    """Per-alternative contribution — used in §3.5 and §5.

    sign(p) = +1 for min, -1 for max (topsis.md §3.5).
    """
    result = TriangularFuzzyM.zero()
    for p in scenario.properties:
        m_p = M[p.id]
        if m_p == 0.0:
            # Invariant 10.1: degenerate — _compute_normalizer already warned
            # once for this property; silently skip here.
            continue
        sign = 1.0 if p.kind == "min" else -1.0
        contribution = coeff[(alt_id, p.id)] * (sign * p.weight) / m_p
        result = result + contribution
    return result


def _candidate_total_value(
    candidate_alt_ids: tuple[str, ...],
    scenario: ScenarioM,
    coeff: dict[tuple[str, str], TriangularFuzzyM],
    M: dict[str, float],
) -> TriangularFuzzyM:
    """Aggregate contribution for a full candidate — topsis.md §3.5."""
    result = TriangularFuzzyM.zero()
    for alt_id in candidate_alt_ids:
        result = result + _alt_contribution(alt_id, scenario, coeff, M)
    return result


def _normalize_candidates(
    z_values: list[NormalizedFuzzyM],
) -> list[NormalizedFuzzyM]:
    """Compute PIS/NIS in Z-space and normalize — topsis.md §3.7, §3.8.

    Weights do not enter the PIS/NIS step (they multiply per-property values
    *before* aggregation in §3.5); earlier drafts carried a `weights` param
    that this function never read. Removed.
    """
    if not z_values:
        return []

    # §3.7 PIS and NIS
    pis_avg = min(z.average for z in z_values)
    pis_pos = max(z.positive for z in z_values)
    pis_neg = min(z.negative for z in z_values)

    nis_avg = max(z.average for z in z_values)
    nis_pos = min(z.positive for z in z_values)
    nis_neg = max(z.negative for z in z_values)

    normalized: list[NormalizedFuzzyM] = []
    for z in z_values:
        # §3.8 — clip01 and 0/0 → 0
        denom_avg = nis_avg - pis_avg
        n_avg = _clip01((z.average - pis_avg) / denom_avg) if denom_avg != 0.0 else 0.0

        denom_pos = pis_pos - nis_pos
        n_pos = _clip01((pis_pos - z.positive) / denom_pos) if denom_pos != 0.0 else 0.0

        denom_neg = nis_neg - pis_neg
        n_neg = _clip01((z.negative - pis_neg) / denom_neg) if denom_neg != 0.0 else 0.0

        normalized.append(NormalizedFuzzyM(positive=n_pos, average=n_avg, negative=n_neg))
    return normalized


def _score(n: NormalizedFuzzyM, weights: NormalizedFuzzyM, aggregation: str) -> float:
    """Compute per-candidate score — topsis.md §3.9, §3.10."""
    phi_pos = weights.positive * n.positive
    phi_avg = weights.average * n.average
    phi_neg = weights.negative * n.negative
    if aggregation == "sum":
        return phi_pos + phi_avg + phi_neg
    else:  # "max"
        return max(phi_pos, phi_avg, phi_neg)


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def solve(scenario: ScenarioM) -> tuple[CandidateM, ...]:
    """Run the full TOPSIS pipeline — topsis.md §3.

    Returns a tuple of CandidateM sorted by score ascending (lower = better),
    with lexicographic tie-breaking on alternative_ids (§3.10).
    """
    # -----------------------------------------------------------------------
    # §3.1  Enumerate raw candidates
    # -----------------------------------------------------------------------
    dec_order = scenario.decisions  # preserve scenario order
    alts_by_dec: dict[str, list[str]] = {}
    for a in scenario.alternatives:
        alts_by_dec.setdefault(a.decision_id, []).append(a.id)

    pools: list[list[str]] = [alts_by_dec.get(d.id, []) for d in dec_order]
    raw_candidates: list[tuple[str, ...]] = [tuple(combo) for combo in itertools.product(*pools)]

    if not raw_candidates:
        return ()

    # -----------------------------------------------------------------------
    # §3.2  Filter candidates
    # -----------------------------------------------------------------------
    dep_constraints = [c for c in scenario.constraints if isinstance(c, DependencyConstraint)]
    conf_constraints = [c for c in scenario.constraints if isinstance(c, ConflictConstraint)]
    thresh_constraints = [c for c in scenario.constraints if isinstance(c, ThresholdConstraint)]

    # Build coeff lookup
    coeff: dict[tuple[str, str], TriangularFuzzyM] = {
        (c.alternative_id, c.property_id): c.value for c in scenario.coefficients
    }
    prop_map = {p.id: p for p in scenario.properties}

    def passes_filters(candidate: tuple[str, ...]) -> bool:
        c_set = set(candidate)

        # 1. Dependency (biconditional) — topsis.md §3.2 step 1
        for dc in dep_constraints:
            src_in = dc.source_alternative_id in c_set
            tgt_in = dc.target_alternative_id in c_set
            if src_in != tgt_in:  # XOR — violates biconditional
                return False

        # 2. Conflict — topsis.md §3.2 step 2
        for cc in conf_constraints:
            if cc.alternative_a_id in c_set and cc.alternative_b_id in c_set:
                return False

        # 3. Threshold — topsis.md §3.2 step 3
        for tc in thresh_constraints:
            contrib = TriangularFuzzyM.zero()
            for a_id in candidate:
                contrib = contrib + coeff[(a_id, tc.property_id)]
            defuzzed = contrib.lower  # §4.2: lower vertex only
            prop = prop_map[tc.property_id]
            if prop.kind == "min":
                if tc.max is not None and defuzzed > tc.max:
                    return False
            else:  # max
                if tc.min is not None and defuzzed < tc.min:
                    return False

        return True

    feasible = [c for c in raw_candidates if passes_filters(c)]
    if not feasible:
        return ()

    # -----------------------------------------------------------------------
    # §3.4  Per-property normalizer (over original alternative pool)
    # -----------------------------------------------------------------------
    M = _compute_normalizer(scenario)
    _warn_degenerate_normalizers(M)

    # -----------------------------------------------------------------------
    # §3.5  Total triangular value per candidate
    # -----------------------------------------------------------------------
    total_values = [_candidate_total_value(c, scenario, coeff, M) for c in feasible]

    # -----------------------------------------------------------------------
    # §3.6  Convert to Z-space
    # -----------------------------------------------------------------------
    z_values = [_to_z(tv) for tv in total_values]

    # -----------------------------------------------------------------------
    # §3.7-3.8  PIS/NIS normalization
    # -----------------------------------------------------------------------
    norm_values = _normalize_candidates(z_values)

    # -----------------------------------------------------------------------
    # §3.9-3.10  Score, sort, rank
    # -----------------------------------------------------------------------
    scores = [_score(n, scenario.config.weights, scenario.config.aggregation) for n in norm_values]

    # Combine and sort: primary key = score ascending; secondary = lex on alt_ids
    indexed = sorted(
        enumerate(feasible),
        key=lambda iv: (scores[iv[0]], iv[1]),
    )

    candidates: list[CandidateM] = []
    for rank, (i, alt_ids) in enumerate(indexed):
        candidates.append(
            CandidateM(
                alternative_ids=alt_ids,
                triangular_value=total_values[i],
                normalized_value=norm_values[i],
                score=scores[i],
                rank=rank,
            )
        )

    return tuple(candidates)
