"""Deterministic JSON serialization for TOPSIS outputs.

Float formatting: Python's default repr (full precision).
Field ordering is stable (insertion order in dict literals).
"""

from __future__ import annotations

import json
from typing import Any

from guidearch.models.candidate import CandidateM
from guidearch.models.critical_constraint import CriticalConstraintM
from guidearch.models.critical_decision import CriticalDecisionM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM


def _tfm_to_dict(t: TriangularFuzzyM) -> dict[str, Any]:
    return {"lower": t.lower, "modal": t.modal, "upper": t.upper}


def _nfm_to_dict(n: NormalizedFuzzyM) -> dict[str, Any]:
    return {"positive": n.positive, "average": n.average, "negative": n.negative}


def candidates_to_dict(candidates: tuple[CandidateM, ...]) -> list[dict[str, Any]]:
    """Serialize a tuple of CandidateM to a list of dicts."""
    return [
        {
            "alternativeIds": list(c.alternative_ids),
            "triangularValue": _tfm_to_dict(c.triangular_value),
            "normalizedValue": _nfm_to_dict(c.normalized_value),
            "score": c.score,
            "rank": c.rank,
        }
        for c in candidates
    ]


def critical_decisions_to_dict(
    decisions: tuple[CriticalDecisionM, ...],
) -> list[dict[str, Any]]:
    """Serialize a tuple of CriticalDecisionM to a list of dicts."""
    return [
        {
            "decisionId": d.decision_id,
            "triangularValue": _tfm_to_dict(d.triangular_value),
            "normalizedValue": _nfm_to_dict(d.normalized_value),
            "score": d.score,
            "rank": d.rank,
        }
        for d in decisions
    ]


def critical_constraints_to_dict(
    constraints: tuple[CriticalConstraintM, ...],
) -> list[dict[str, Any]]:
    """Serialize a tuple of CriticalConstraintM to a list of dicts."""
    return [
        {
            "constraintIndex": c.constraint_index,
            "kind": c.kind,
            "eliminated": c.eliminated,
            "total": c.total,
            "redundant": c.redundant,
        }
        for c in constraints
    ]


def to_json(obj: Any) -> str:
    """Pretty-print to deterministic JSON (indent=2, sorted_keys=False)."""
    return json.dumps(obj, indent=2, ensure_ascii=False)
