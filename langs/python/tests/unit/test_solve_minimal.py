"""Minimal hand-computable scenario test for solve() — topsis.md §3.

Scenario: 1 decision, 2 alternatives, 1 property (min, weight=1).
  Alt A: coeff = (1, 1, 1)   <- smaller is better for min property
  Alt B: coeff = (2, 2, 2)

For a single-alternative candidate, the candidate *is* the alternative.
Config: aggregation=max, weights=(1/3, 1/3, 1/3).

Manual derivation
-----------------
§3.4  M(p) = max_{per decision} coefficient.lower (for min property)
           = max(1, 2) = 2

§3.5  sign(p) = +1 for min.

  totalValue(A) = (1 * 1 * (1,1,1)) / 2 = (0.5, 0.5, 0.5)
  totalValue(B) = (1 * 1 * (2,2,2)) / 2 = (1.0, 1.0, 1.0)

§3.6  toZ:
  z(A) = {positive: |0.5-0.5|=0, average: 0.5, negative: |0.5-0.5|=0}
  z(B) = {positive: 0,            average: 1.0, negative: 0}

§3.7  PIS.average = min(0.5, 1.0) = 0.5
      NIS.average = max(0.5, 1.0) = 1.0
      PIS.positive = max(0, 0) = 0   NIS.positive = min(0,0) = 0
      PIS.negative = min(0, 0) = 0   NIS.negative = max(0,0) = 0

§3.8  n.average(A) = (0.5-0.5)/(1.0-0.5) = 0.0
      n.average(B) = (1.0-0.5)/0.5 = 1.0
      n.positive -- denom=0 -> 0 for both
      n.negative -- denom=0 -> 0 for both

§3.9  phi_pos=phi_neg=0.  phi_avg(A)=1/3*0=0; phi_avg(B)=1/3*1=1/3

§3.10 score(A) = max(0, 0, 0) = 0   <- lower is better
      score(B) = max(0, 1/3, 0) = 1/3
      rank(A)=0, rank(B)=1
"""

from guidearch.models.alternative import AlternativeM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.constraint import ConflictConstraint
from guidearch.models.decision import DecisionM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.property import PropertyM
from guidearch.models.scenario import ConfigM, ScenarioM
from guidearch.models.topsis.solve import solve
from guidearch.models.triangular_fuzzy import TriangularFuzzyM

_TOL = 1e-12
_W = NormalizedFuzzyM(positive=1 / 3, average=1 / 3, negative=1 / 3)
_TFM_111 = TriangularFuzzyM(1.0, 1.0, 1.0)
_TFM_222 = TriangularFuzzyM(2.0, 2.0, 2.0)


def _build_minimal_scenario() -> ScenarioM:
    d1 = DecisionM(id="d1", name="Decision 1")
    a1 = AlternativeM(id="a1", decision_id="d1", name="Alt A")
    a2 = AlternativeM(id="a2", decision_id="d1", name="Alt B")
    p1 = PropertyM(id="p1", name="Prop 1", kind="min", weight=1.0)
    coeff_a = CoefficientM(alternative_id="a1", property_id="p1", value=_TFM_111)
    coeff_b = CoefficientM(alternative_id="a2", property_id="p1", value=_TFM_222)
    config = ConfigM(aggregation="max", weights=_W)
    return ScenarioM(
        schema_version="1.0.0",
        name="minimal",
        description="",
        decisions=(d1,),
        alternatives=(a1, a2),
        properties=(p1,),
        coefficients=(coeff_a, coeff_b),
        constraints=(),
        config=config,
    )


def test_minimal_solve_returns_two_candidates() -> None:
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    assert len(candidates) == 2


def test_minimal_solve_rank_order() -> None:
    """Alt A (smaller coeff) should rank 0; Alt B should rank 1."""
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    assert candidates[0].alternative_ids == ("a1",)
    assert candidates[1].alternative_ids == ("a2",)
    assert candidates[0].rank == 0
    assert candidates[1].rank == 1


def test_minimal_solve_scores() -> None:
    """score(A) = 0.0, score(B) = 1/3."""
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    assert abs(candidates[0].score - 0.0) < _TOL
    assert abs(candidates[1].score - 1 / 3) < _TOL


def test_minimal_solve_triangular_values() -> None:
    """§3.5: totalValue(A) = (0.5,0.5,0.5), totalValue(B) = (1.0,1.0,1.0)."""
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    tv_a = candidates[0].triangular_value
    tv_b = candidates[1].triangular_value
    for v in (tv_a.lower, tv_a.modal, tv_a.upper):
        assert abs(v - 0.5) < _TOL
    for v in (tv_b.lower, tv_b.modal, tv_b.upper):
        assert abs(v - 1.0) < _TOL


def test_minimal_solve_normalized_values() -> None:
    """§3.8: n.average(A)=0, n.average(B)=1; positive/negative=0 for both."""
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    nv_a = candidates[0].normalized_value
    nv_b = candidates[1].normalized_value
    assert abs(nv_a.average - 0.0) < _TOL
    assert abs(nv_b.average - 1.0) < _TOL
    assert abs(nv_a.positive - 0.0) < _TOL
    assert abs(nv_b.positive - 0.0) < _TOL


def test_minimal_solve_lower_score_is_better() -> None:
    """score(rank=0) <= score(rank=1) — topsis.md §3.10."""
    scenario = _build_minimal_scenario()
    candidates = solve(scenario)
    for i in range(len(candidates) - 1):
        assert candidates[i].score <= candidates[i + 1].score


def test_minimal_max_property() -> None:
    """Invert to max: larger coeff should now be preferred (rank=0)."""
    d1 = DecisionM(id="d1", name="Decision 1")
    a1 = AlternativeM(id="a1", decision_id="d1", name="Alt A")
    a2 = AlternativeM(id="a2", decision_id="d1", name="Alt B")
    p1 = PropertyM(id="p1", name="Prop 1", kind="max", weight=1.0)
    coeff_a = CoefficientM(alternative_id="a1", property_id="p1", value=_TFM_111)
    coeff_b = CoefficientM(alternative_id="a2", property_id="p1", value=_TFM_222)
    config = ConfigM(aggregation="max", weights=_W)
    scenario = ScenarioM(
        schema_version="1.0.0",
        name="minimal_max",
        description="",
        decisions=(d1,),
        alternatives=(a1, a2),
        properties=(p1,),
        coefficients=(coeff_a, coeff_b),
        constraints=(),
        config=config,
    )
    candidates = solve(scenario)
    # Alt B has higher value on a max property -> should rank better (rank=0)
    assert candidates[0].alternative_ids == ("a2",)


def test_no_feasible_candidates_returns_empty() -> None:
    """Conflicts eliminating all candidates return empty tuple."""
    d1 = DecisionM(id="d1", name="Decision 1")
    a1 = AlternativeM(id="a1", decision_id="d1", name="Alt A")
    a2 = AlternativeM(id="a2", decision_id="d1", name="Alt B")
    p1 = PropertyM(id="p1", name="Prop 1", kind="min", weight=1.0)
    coeff_a = CoefficientM(alternative_id="a1", property_id="p1", value=_TFM_111)
    coeff_b = CoefficientM(alternative_id="a2", property_id="p1", value=_TFM_222)
    config = ConfigM(aggregation="max", weights=_W)

    # Need a second decision to create actual conflicts that eliminate everything
    d2 = DecisionM(id="d2", name="Decision 2")
    a3 = AlternativeM(id="a3", decision_id="d2", name="Alt C")
    a4 = AlternativeM(id="a4", decision_id="d2", name="Alt D")
    coeff_c = CoefficientM(alternative_id="a3", property_id="p1", value=_TFM_111)
    coeff_d = CoefficientM(alternative_id="a4", property_id="p1", value=_TFM_111)

    # All four combinations of (a1|a2, a3|a4) are conflicted
    constraints: tuple[ConflictConstraint, ...] = (
        ConflictConstraint(kind="conflict", alternative_a_id="a1", alternative_b_id="a3"),
        ConflictConstraint(kind="conflict", alternative_a_id="a1", alternative_b_id="a4"),
        ConflictConstraint(kind="conflict", alternative_a_id="a2", alternative_b_id="a3"),
        ConflictConstraint(kind="conflict", alternative_a_id="a2", alternative_b_id="a4"),
    )
    scenario = ScenarioM(
        schema_version="1.0.0",
        name="empty",
        description="",
        decisions=(d1, d2),
        alternatives=(a1, a2, a3, a4),
        properties=(p1,),
        coefficients=(coeff_a, coeff_b, coeff_c, coeff_d),
        constraints=constraints,
        config=config,
    )
    candidates = solve(scenario)
    assert candidates == ()
