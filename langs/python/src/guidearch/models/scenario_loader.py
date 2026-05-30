"""Load and validate a scenario JSON file → ScenarioM.

Path depth (from scenario_loader.py to repo root):
  scenario_loader.py → models(0) → guidearch(1) → src(2) → python(3) → langs(4) → root(5)
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import jsonschema

from guidearch.models.alternative import AlternativeM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.constraint import (
    ConflictConstraint,
    Constraint,
    DependencyConstraint,
    ThresholdConstraint,
)
from guidearch.models.decision import DecisionM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.property import PropertyM
from guidearch.models.scenario import ConfigM, ScenarioM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM

_REPO_ROOT = Path(__file__).parents[5]
_DEFAULT_SCHEMA_PATH = _REPO_ROOT / "spec" / "domain" / "scenario.schema.json"


class ScenarioValidationError(ValueError):
    """Raised for fatal invariant violations in a scenario."""


def load_scenario(
    path: Path,
    schema_path: Path | None = None,
) -> ScenarioM:
    """Load, validate, and parse a scenario JSON file.

    Args:
        path: Path to the scenario JSON.
        schema_path: Override the JSON Schema path (optional).

    Returns:
        A validated ScenarioM with any warning messages in `.warnings`.

    Raises:
        ScenarioValidationError: on any fatal invariant violation.
    """
    schema_path = schema_path or _DEFAULT_SCHEMA_PATH

    raw = json.loads(path.read_text(encoding="utf-8"))

    # ------------------------------------------------------------------ #
    # JSON Schema structural validation
    # ------------------------------------------------------------------ #
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    validator = jsonschema.Draft202012Validator(schema)
    errors = list(validator.iter_errors(raw))
    if errors:
        msgs = "; ".join(e.message for e in errors[:5])
        raise ScenarioValidationError(f"JSON Schema validation failed: {msgs}")

    warnings: list[str] = []

    # ------------------------------------------------------------------ #
    # Parse raw objects
    # ------------------------------------------------------------------ #
    decisions = tuple(
        DecisionM(id=d["id"], name=d["name"]) for d in raw["decisions"]
    )
    alternatives = tuple(
        AlternativeM(id=a["id"], decision_id=a["decisionId"], name=a["name"])
        for a in raw["alternatives"]
    )
    properties = tuple(
        PropertyM(id=p["id"], name=p["name"], kind=p["kind"], weight=p["weight"])
        for p in raw["properties"]
    )

    def _tfm(v: dict[str, Any]) -> TriangularFuzzyM:
        return TriangularFuzzyM(
            lower=float(v["lower"]),
            modal=float(v["modal"]),
            upper=float(v["upper"]),
        )

    coefficients = tuple(
        CoefficientM(
            alternative_id=c["alternativeId"],
            property_id=c["propertyId"],
            value=_tfm(c["value"]),
        )
        for c in raw["coefficients"]
    )

    constraints: list[Constraint] = []
    for c in raw["constraints"]:
        if c["kind"] == "threshold":
            constraints.append(
                ThresholdConstraint(
                    kind="threshold",
                    property_id=c["propertyId"],
                    min=c.get("min"),
                    max=c.get("max"),
                )
            )
        elif c["kind"] == "dependency":
            constraints.append(
                DependencyConstraint(
                    kind="dependency",
                    source_alternative_id=c["sourceAlternativeId"],
                    target_alternative_id=c["targetAlternativeId"],
                )
            )
        elif c["kind"] == "conflict":
            constraints.append(
                ConflictConstraint(
                    kind="conflict",
                    alternative_a_id=c["alternativeAId"],
                    alternative_b_id=c["alternativeBId"],
                )
            )

    w_raw = raw["config"]["weights"]
    weights = NormalizedFuzzyM(
        positive=float(w_raw["positive"]),
        average=float(w_raw["average"]),
        negative=float(w_raw["negative"]),
    )
    config = ConfigM(
        aggregation=raw["config"]["aggregation"],
        weights=weights,
    )

    # ------------------------------------------------------------------ #
    # Invariant 1: Identifier uniqueness (fatal)
    # ------------------------------------------------------------------ #
    dec_ids = [d.id for d in decisions]
    alt_ids = [a.id for a in alternatives]
    prop_ids = [p.id for p in properties]

    if len(dec_ids) != len(set(dec_ids)):
        raise ScenarioValidationError("Invariant 1.1: duplicate decision id(s)")
    if len(alt_ids) != len(set(alt_ids)):
        raise ScenarioValidationError("Invariant 1.2: duplicate alternative id(s)")
    if len(prop_ids) != len(set(prop_ids)):
        raise ScenarioValidationError("Invariant 1.3: duplicate property id(s)")

    dec_id_set = set(dec_ids)
    alt_id_set = set(alt_ids)
    prop_id_set = set(prop_ids)

    overlap_da = dec_id_set & alt_id_set
    overlap_dp = dec_id_set & prop_id_set
    overlap_ap = alt_id_set & prop_id_set
    if overlap_da or overlap_dp or overlap_ap:
        raise ScenarioValidationError(
            f"Invariant 1.4: id namespace collision: "
            f"d∩a={overlap_da}, d∩p={overlap_dp}, a∩p={overlap_ap}"
        )

    # ------------------------------------------------------------------ #
    # Invariant 2: Cross-reference validity (fatal)
    # ------------------------------------------------------------------ #
    for a in alternatives:
        if a.decision_id not in dec_id_set:
            raise ScenarioValidationError(
                f"Invariant 2.1: alternative '{a.id}' references unknown "
                f"decision '{a.decision_id}'"
            )
    for c in coefficients:
        if c.alternative_id not in alt_id_set:
            raise ScenarioValidationError(
                f"Invariant 2.2: coefficient references unknown alternative "
                f"'{c.alternative_id}'"
            )
        if c.property_id not in prop_id_set:
            raise ScenarioValidationError(
                f"Invariant 2.3: coefficient references unknown property "
                f"'{c.property_id}'"
            )
    for con in constraints:
        if isinstance(con, ThresholdConstraint):
            if con.property_id not in prop_id_set:
                raise ScenarioValidationError(
                    f"Invariant 2.4: threshold constraint references unknown "
                    f"property '{con.property_id}'"
                )
        elif isinstance(con, DependencyConstraint):
            if con.source_alternative_id not in alt_id_set:
                raise ScenarioValidationError(
                    f"Invariant 2.5: dependency constraint references unknown "
                    f"source '{con.source_alternative_id}'"
                )
            if con.target_alternative_id not in alt_id_set:
                raise ScenarioValidationError(
                    f"Invariant 2.5: dependency constraint references unknown "
                    f"target '{con.target_alternative_id}'"
                )
        elif isinstance(con, ConflictConstraint):
            if con.alternative_a_id not in alt_id_set:
                raise ScenarioValidationError(
                    f"Invariant 2.5: conflict constraint references unknown "
                    f"alternative A '{con.alternative_a_id}'"
                )
            if con.alternative_b_id not in alt_id_set:
                raise ScenarioValidationError(
                    f"Invariant 2.5: conflict constraint references unknown "
                    f"alternative B '{con.alternative_b_id}'"
                )

    # ------------------------------------------------------------------ #
    # Invariant 3: Coefficient completeness (fatal)
    # ------------------------------------------------------------------ #
    coeff_pairs = [(c.alternative_id, c.property_id) for c in coefficients]
    coeff_pair_set = set(coeff_pairs)
    if len(coeff_pairs) != len(coeff_pair_set):
        raise ScenarioValidationError(
            "Invariant 3.1: duplicate (alternativeId, propertyId) coefficient"
        )
    for a_id in alt_ids:
        for p_id in prop_ids:
            if (a_id, p_id) not in coeff_pair_set:
                raise ScenarioValidationError(
                    f"Invariant 3.1: missing coefficient for "
                    f"(alternative={a_id}, property={p_id})"
                )

    # ------------------------------------------------------------------ #
    # Invariant 4: Triangular ordering (warning)
    # ------------------------------------------------------------------ #
    for c in coefficients:
        v = c.value
        if not (v.lower <= v.modal <= v.upper):
            warnings.append(
                f"Invariant 4.1: coefficient ({c.alternative_id}, "
                f"{c.property_id}) has lower={v.lower} modal={v.modal} "
                f"upper={v.upper} — ordering violated"
            )

    # ------------------------------------------------------------------ #
    # Invariant 5: Weights (fatal)
    # ------------------------------------------------------------------ #
    w = config.weights
    for val, label in [(w.positive, "positive"), (w.average, "average"), (w.negative, "negative")]:
        if not (0.0 <= val <= 1.0):
            raise ScenarioValidationError(
                f"Invariant 5.2: weight.{label}={val} is outside [0, 1]"
            )
    w_sum = w.positive + w.average + w.negative
    if abs(w_sum - 1.0) > 1e-9:
        raise ScenarioValidationError(
            f"Invariant 5.1: weights sum to {w_sum}, expected 1.0 (tolerance 1e-9)"
        )

    # ------------------------------------------------------------------ #
    # Invariant 6: Threshold constraints (fatal)
    # ------------------------------------------------------------------ #
    for i, con in enumerate(constraints):
        if isinstance(con, ThresholdConstraint):
            if con.min is None and con.max is None:
                raise ScenarioValidationError(
                    f"Invariant 6.1: threshold constraint[{i}] has neither min nor max"
                )
            if con.min is not None and con.max is not None and con.min > con.max:
                raise ScenarioValidationError(
                    f"Invariant 6.2: threshold constraint[{i}] has min={con.min} > "
                    f"max={con.max}"
                )

    # ------------------------------------------------------------------ #
    # Invariant 7: Dependency / conflict self-edges (fatal + warning)
    # ------------------------------------------------------------------ #
    dec_of: dict[str, str] = {a.id: a.decision_id for a in alternatives}
    for i, con in enumerate(constraints):
        if isinstance(con, DependencyConstraint):
            if con.source_alternative_id == con.target_alternative_id:
                raise ScenarioValidationError(
                    f"Invariant 7.1: dependency constraint[{i}] is a self-edge"
                )
            if dec_of.get(con.source_alternative_id) == dec_of.get(
                con.target_alternative_id
            ):
                warnings.append(
                    f"Invariant 7.2: dependency constraint[{i}] connects "
                    f"alternatives of the same decision — rarely meaningful"
                )
        elif isinstance(con, ConflictConstraint):
            if con.alternative_a_id == con.alternative_b_id:
                raise ScenarioValidationError(
                    f"Invariant 7.1: conflict constraint[{i}] is a self-edge"
                )
            if dec_of.get(con.alternative_a_id) == dec_of.get(
                con.alternative_b_id
            ):
                warnings.append(
                    f"Invariant 7.2: conflict constraint[{i}] connects "
                    f"alternatives of the same decision — rarely meaningful"
                )

    # ------------------------------------------------------------------ #
    # Invariant 8: Decision occupancy (fatal)
    # ------------------------------------------------------------------ #
    alts_by_dec: dict[str, list[str]] = {d.id: [] for d in decisions}
    for a in alternatives:
        alts_by_dec[a.decision_id].append(a.id)
    for d in decisions:
        if not alts_by_dec[d.id]:
            raise ScenarioValidationError(
                f"Invariant 8.1: decision '{d.id}' ('{d.name}') has no alternatives"
            )

    return ScenarioM(
        schema_version=raw["schemaVersion"],
        name=raw["name"],
        description=raw.get("description", ""),
        decisions=decisions,
        alternatives=alternatives,
        properties=properties,
        coefficients=coefficients,
        constraints=tuple(constraints),
        config=config,
        warnings=tuple(warnings),
    )
