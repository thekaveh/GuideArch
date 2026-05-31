"""Conformance runner — compares solver output against spec/conformance/expected/*.

CLI entry point: python -m guidearch.conformance.runner
  Exits 0 on conformance, 1 on divergence.
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from guidearch.models.scenario_loader import load_scenario
from guidearch.models.topsis.critical_constraints import critical_constraints
from guidearch.models.topsis.critical_decisions import critical_decisions
from guidearch.models.topsis.solve import solve
from guidearch.output.serialize import (
    candidates_to_dict,
    critical_constraints_to_dict,
    critical_decisions_to_dict,
)

# Path to the conformance corpus. The dev / in-tree walk works the same way
# scenario_loader._load_default_schema does (parents[5] is the repo root from
# this module). Wheel-installed callers won't have spec/ alongside the package;
# scenarios + expected files are not bundled into the wheel, so wheel users
# must point GUIDEARCH_SPEC_DIR at a checkout. The env-var override keeps the
# CLI usable from outside the repo without requiring an in-tree check-in.
_DEFAULT_SPEC_DIR = Path(__file__).parents[5] / "spec" / "conformance"
_SPEC_DIR = Path(__import__("os").environ.get("GUIDEARCH_SPEC_DIR", str(_DEFAULT_SPEC_DIR)))


def _load_abs_tol(spec_dir: Path) -> float:
    """Read the scalar absolute tolerance from spec/conformance/tolerances.json.

    All scalar fields in the corpus share the same absolute tolerance at
    v1.0; tolerances.json was the single source of truth for TS already.
    Falling back to 1e-9 keeps the runner usable when the corpus is missing
    (e.g. wheel install without GUIDEARCH_SPEC_DIR).
    """
    tol_path = spec_dir / "tolerances.json"
    if not tol_path.exists():
        return 1e-9
    data = json.loads(tol_path.read_text(encoding="utf-8"))
    return float(data["candidates"]["score"]["absolute"])


_ABS_TOL = _load_abs_tol(_SPEC_DIR)


@dataclass
class Difference:
    """A single conformance failure."""

    scenario: str
    kind: str  # 'candidates' | 'critical-decisions' | 'critical-constraints'
    index: int
    field: str
    expected: object
    actual: object

    def __str__(self) -> str:
        return (
            f"[{self.scenario}] {self.kind}[{self.index}].{self.field}: "
            f"expected={self.expected!r}, actual={self.actual!r}"
        )


def _float_close(a: float, b: float, tol: float = _ABS_TOL) -> bool:
    return abs(a - b) <= tol


def _compare_tfm(
    exp: dict[str, Any],
    act: dict[str, Any],
    diffs: list[Difference],
    scenario: str,
    kind: str,
    idx: int,
    prefix: str,
) -> None:
    for key in ("lower", "modal", "upper"):
        if not _float_close(exp[key], act[key]):
            diffs.append(Difference(scenario, kind, idx, f"{prefix}.{key}", exp[key], act[key]))


def _compare_nfm(
    exp: dict[str, Any],
    act: dict[str, Any],
    diffs: list[Difference],
    scenario: str,
    kind: str,
    idx: int,
    prefix: str,
) -> None:
    for key in ("positive", "average", "negative"):
        if not _float_close(exp[key], act[key]):
            diffs.append(Difference(scenario, kind, idx, f"{prefix}.{key}", exp[key], act[key]))


def _compare_candidates(
    exp_list: list[dict[str, Any]],
    act_list: list[dict[str, Any]],
    diffs: list[Difference],
    scenario: str,
) -> None:
    kind = "candidates"
    if len(exp_list) != len(act_list):
        diffs.append(Difference(scenario, kind, -1, "count", len(exp_list), len(act_list)))
        return
    for i, (exp, act) in enumerate(zip(exp_list, act_list, strict=True)):
        if exp["alternativeIds"] != act["alternativeIds"]:
            diffs.append(
                Difference(
                    scenario,
                    kind,
                    i,
                    "alternativeIds",
                    exp["alternativeIds"],
                    act["alternativeIds"],
                )
            )
        _compare_tfm(
            exp["triangularValue"],
            act["triangularValue"],
            diffs,
            scenario,
            kind,
            i,
            "triangularValue",
        )
        _compare_nfm(
            exp["normalizedValue"],
            act["normalizedValue"],
            diffs,
            scenario,
            kind,
            i,
            "normalizedValue",
        )
        if not _float_close(exp["score"], act["score"]):
            diffs.append(Difference(scenario, kind, i, "score", exp["score"], act["score"]))
        if exp["rank"] != act["rank"]:
            diffs.append(Difference(scenario, kind, i, "rank", exp["rank"], act["rank"]))


def _compare_critical_decisions(
    exp_list: list[dict[str, Any]],
    act_list: list[dict[str, Any]],
    diffs: list[Difference],
    scenario: str,
) -> None:
    kind = "critical-decisions"
    if len(exp_list) != len(act_list):
        diffs.append(Difference(scenario, kind, -1, "count", len(exp_list), len(act_list)))
        return
    for i, (exp, act) in enumerate(zip(exp_list, act_list, strict=True)):
        if exp["decisionId"] != act["decisionId"]:
            diffs.append(
                Difference(
                    scenario,
                    kind,
                    i,
                    "decisionId",
                    exp["decisionId"],
                    act["decisionId"],
                )
            )
        _compare_tfm(
            exp["triangularValue"],
            act["triangularValue"],
            diffs,
            scenario,
            kind,
            i,
            "triangularValue",
        )
        _compare_nfm(
            exp["normalizedValue"],
            act["normalizedValue"],
            diffs,
            scenario,
            kind,
            i,
            "normalizedValue",
        )
        if not _float_close(exp["score"], act["score"]):
            diffs.append(Difference(scenario, kind, i, "score", exp["score"], act["score"]))
        if exp["rank"] != act["rank"]:
            diffs.append(Difference(scenario, kind, i, "rank", exp["rank"], act["rank"]))


def _compare_critical_constraints(
    exp_list: list[dict[str, Any]],
    act_list: list[dict[str, Any]],
    diffs: list[Difference],
    scenario: str,
) -> None:
    kind = "critical-constraints"
    if len(exp_list) != len(act_list):
        diffs.append(Difference(scenario, kind, -1, "count", len(exp_list), len(act_list)))
        return
    for i, (exp, act) in enumerate(zip(exp_list, act_list, strict=True)):
        for field in ("constraintIndex", "kind", "eliminated", "total", "redundant"):
            if exp[field] != act[field]:
                diffs.append(Difference(scenario, kind, i, field, exp[field], act[field]))


def run_conformance(spec_dir: Path = _SPEC_DIR) -> list[Difference]:
    """Run conformance for all scenarios in spec_dir/scenarios/*.json.

    Compares against spec_dir/expected/<name>.{candidates,critical-decisions,
    critical-constraints}.json, honoring tolerances.json.
    """
    scenarios_dir = spec_dir / "scenarios"
    expected_dir = spec_dir / "expected"

    all_diffs: list[Difference] = []

    for scenario_path in sorted(scenarios_dir.glob("*.json")):
        name = scenario_path.stem.lower()

        scenario = load_scenario(scenario_path)
        candidates = solve(scenario)
        cd = critical_decisions(scenario, candidates)
        cc = critical_constraints(scenario)

        actual_candidates = candidates_to_dict(candidates)
        actual_cd = critical_decisions_to_dict(cd)
        actual_cc = critical_constraints_to_dict(cc)

        cand_exp_path = expected_dir / f"{name}.candidates.json"
        cd_exp_path = expected_dir / f"{name}.critical-decisions.json"
        cc_exp_path = expected_dir / f"{name}.critical-constraints.json"

        if cand_exp_path.exists():
            exp_candidates = json.loads(cand_exp_path.read_text(encoding="utf-8"))
            _compare_candidates(exp_candidates, actual_candidates, all_diffs, name)
        else:
            all_diffs.append(
                Difference(
                    name,
                    "candidates",
                    -1,
                    "expected_file",
                    str(cand_exp_path),
                    "missing",
                )
            )

        if cd_exp_path.exists():
            exp_cd = json.loads(cd_exp_path.read_text(encoding="utf-8"))
            _compare_critical_decisions(exp_cd, actual_cd, all_diffs, name)
        else:
            all_diffs.append(
                Difference(
                    name,
                    "critical-decisions",
                    -1,
                    "expected_file",
                    str(cd_exp_path),
                    "missing",
                )
            )

        if cc_exp_path.exists():
            exp_cc = json.loads(cc_exp_path.read_text(encoding="utf-8"))
            _compare_critical_constraints(exp_cc, actual_cc, all_diffs, name)
        else:
            all_diffs.append(
                Difference(
                    name,
                    "critical-constraints",
                    -1,
                    "expected_file",
                    str(cc_exp_path),
                    "missing",
                )
            )

    return all_diffs


def main() -> int:
    diffs = run_conformance()
    if not diffs:
        print("PASS: all conformance checks passed")
        return 0
    print(f"FAIL: {len(diffs)} conformance difference(s):")
    for d in diffs:
        print(f"  {d}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
