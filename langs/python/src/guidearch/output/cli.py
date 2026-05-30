"""CLI for generating expected conformance outputs.

Usage:
    python -m guidearch.output.cli <name> <scenario.json> <output_dir/>

Writes:
    <output_dir>/<name>.candidates.json
    <output_dir>/<name>.critical-decisions.json
    <output_dir>/<name>.critical-constraints.json
"""

from __future__ import annotations

import sys
from pathlib import Path

from guidearch.models.scenario_loader import load_scenario
from guidearch.models.topsis.critical_constraints import critical_constraints
from guidearch.models.topsis.critical_decisions import critical_decisions
from guidearch.models.topsis.solve import solve
from guidearch.output.serialize import (
    candidates_to_dict,
    critical_constraints_to_dict,
    critical_decisions_to_dict,
    to_json,
)


def main(argv: list[str] | None = None) -> int:
    args = argv if argv is not None else sys.argv[1:]
    if len(args) != 3:
        print(
            "Usage: python -m guidearch.output.cli <name> <scenario.json> <output_dir/>",
            file=sys.stderr,
        )
        return 2

    name, scenario_path_str, output_dir_str = args
    scenario_path = Path(scenario_path_str)
    output_dir = Path(output_dir_str)
    output_dir.mkdir(parents=True, exist_ok=True)

    scenario = load_scenario(scenario_path)
    if scenario.warnings:
        for w in scenario.warnings:
            print(f"WARNING: {w}", file=sys.stderr)

    candidates = solve(scenario)
    cd = critical_decisions(scenario, candidates)
    cc = critical_constraints(scenario)

    (output_dir / f"{name}.candidates.json").write_text(
        to_json(candidates_to_dict(candidates)), encoding="utf-8"
    )
    (output_dir / f"{name}.critical-decisions.json").write_text(
        to_json(critical_decisions_to_dict(cd)), encoding="utf-8"
    )
    (output_dir / f"{name}.critical-constraints.json").write_text(
        to_json(critical_constraints_to_dict(cc)), encoding="utf-8"
    )

    print(
        f"Generated {name}.candidates.json ({len(candidates)} candidates), "
        f"{name}.critical-decisions.json ({len(cd)} decisions), "
        f"{name}.critical-constraints.json ({len(cc)} constraints)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
