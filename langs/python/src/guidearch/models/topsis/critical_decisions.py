"""Critical decisions analysis — topsis.md §5."""

from __future__ import annotations

import math
from typing import TYPE_CHECKING

from guidearch.models.candidate import CandidateM
from guidearch.models.critical_decision import CriticalDecisionM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.topsis.solve import (
    _alt_contribution,
    _clip01,
    _compute_normalizer,
    _to_z,
)
from guidearch.models.triangular_fuzzy import TriangularFuzzyM

if TYPE_CHECKING:
    from guidearch.models.scenario import ScenarioM

_TOP_N = 20  # topsis.md §8
_DECAY = 0.1  # topsis.md §8 — exp(-0.1 * rank)


def critical_decisions(
    scenario: ScenarioM,
    candidates: tuple[CandidateM, ...] | None = None,
) -> tuple[CriticalDecisionM, ...]:
    """Rank decisions by how strongly they drive the top candidates' scores.

    Implements topsis.md §5.  Aggregation is always 'max' (legacy hardcodes
    Max here, ignoring config.aggregation — topsis.md §5 last paragraph).

    Returns decisions sorted by score ascending (lower = more critical).
    """
    if candidates is None:
        from guidearch.models.topsis.solve import solve

        candidates = solve(scenario)

    if not candidates:
        return ()

    # Take top-N ranked candidates
    top_n = candidates[: min(_TOP_N, len(candidates))]

    # Build coeff lookup and normalizer
    coeff = {(c.alternative_id, c.property_id): c.value for c in scenario.coefficients}
    M = _compute_normalizer(scenario)

    # Map decision_id → index in scenario.decisions
    dec_index: dict[str, int] = {d.id: i for i, d in enumerate(scenario.decisions)}

    # -----------------------------------------------------------------------
    # §5  contribution(di) = sum over c in Rt : exp(-0.1*rank(c)) * altContrib(c[i])
    # -----------------------------------------------------------------------
    dec_contributions: dict[str, TriangularFuzzyM] = {
        d.id: TriangularFuzzyM.zero() for d in scenario.decisions
    }

    for c in top_n:
        weight_factor = math.exp(-_DECAY * c.rank)
        for d in scenario.decisions:
            # Find the alternative in this candidate for decision d
            d_idx = dec_index[d.id]
            alt_id = c.alternative_ids[d_idx]
            alt_contrib = _alt_contribution(alt_id, scenario, coeff, M)
            dec_contributions[d.id] = dec_contributions[d.id] + (
                alt_contrib * weight_factor
            )

    # -----------------------------------------------------------------------
    # Convert to Z-space, compute PIS/NIS, normalize -- same as §3.6-3.8
    # but over the *decision* set, with aggregation = max.
    # -----------------------------------------------------------------------
    decision_ids = [d.id for d in scenario.decisions]
    triangular_values = [dec_contributions[d_id] for d_id in decision_ids]
    z_values = [_to_z(tv) for tv in triangular_values]

    if not z_values:
        return ()

    # PIS/NIS over decision set
    pis_avg = min(z.average for z in z_values)
    pis_pos = max(z.positive for z in z_values)
    pis_neg = min(z.negative for z in z_values)

    nis_avg = max(z.average for z in z_values)
    nis_pos = min(z.positive for z in z_values)
    nis_neg = max(z.negative for z in z_values)

    norm_values: list[NormalizedFuzzyM] = []
    for z in z_values:
        denom_avg = nis_avg - pis_avg
        n_avg = _clip01((z.average - pis_avg) / denom_avg) if denom_avg != 0.0 else 0.0

        denom_pos = pis_pos - nis_pos
        n_pos = _clip01((pis_pos - z.positive) / denom_pos) if denom_pos != 0.0 else 0.0

        denom_neg = nis_neg - pis_neg
        n_neg = _clip01((z.negative - pis_neg) / denom_neg) if denom_neg != 0.0 else 0.0

        norm_values.append(NormalizedFuzzyM(positive=n_pos, average=n_avg, negative=n_neg))

    weights = scenario.config.weights

    # Score with max aggregation (legacy hardcodes Max here)
    def _score_max(n: NormalizedFuzzyM) -> float:
        return max(
            weights.positive * n.positive,
            weights.average * n.average,
            weights.negative * n.negative,
        )

    scores = [_score_max(n) for n in norm_values]

    # Sort ascending by score, tie-break by decision_id lexicographic
    indexed = sorted(
        enumerate(decision_ids),
        key=lambda iv: (scores[iv[0]], iv[1]),
    )

    result: list[CriticalDecisionM] = []
    for rank, (i, d_id) in enumerate(indexed):
        result.append(
            CriticalDecisionM(
                decision_id=d_id,
                triangular_value=triangular_values[i],
                normalized_value=norm_values[i],
                score=scores[i],
                rank=rank,
            )
        )

    return tuple(result)
