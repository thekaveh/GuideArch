"""
One-shot importer: legacy GuideArch Space XML → spec/conformance/scenarios/*.json.

The legacy `.Old/GuideArch.Model/Space.cs` persisted its `Space` data as XML
shaped like:

    <Space Name="…">
      <Properties>
        <Property Id="<guid>" Name="…" PropertyType="Min|Max" Priority="<int>" />…
      </Properties>
      <Decisions>
        <Decision Id="<guid>" Name="…" />…
      </Decisions>
      <Alternatives>
        <Alternative Id="<guid>" Name="…" DecisionId="<guid>">
          <Coefficients>
            <Coefficient PropertyId="<guid>">
              <FuzzyValue O="…" A="…" P="…" />
            </Coefficient>…
          </Coefficients>
        </Alternative>…
      </Alternatives>
      <Constraints>
        <PropertyThresholdConstraint PropertyId="<guid>" Threshold="…" />…
        <AlternativeDependencyConstraint DependeeId="<guid>" DependantId="<guid>" />…
        <AlternativeConflictConstraint Alternative1Id="<guid>" Alternative2Id="<guid>" />…
      </Constraints>
    </Space>

The legacy XML stores no `<Config>` — preset = SolutionApproach.Normal +
PhiCalculationApproach.Max. We inject those defaults.

Legacy ids are raw GUIDs and would violate the JSON schema's id pattern
`^[a-zA-Z][a-zA-Z0-9_-]*$` (GUIDs may start with a digit). We rewrite each
id with a stable prefix: `d-…` for decisions, `a-…` for alternatives, `p-…`
for properties. The dashes inside the GUID are preserved for readability.

Usage:
    python tools/import-legacy-xml.py <input.xml> -o <output.json>
"""
from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any


def _slug(guid: str, prefix: str) -> str:
    """Render a legacy GUID as a schema-valid id with a category prefix."""
    return f"{prefix}-{guid}"


def _f(node: ET.Element, attr: str) -> float:
    val = node.get(attr)
    if val is None:
        raise ValueError(f"<{node.tag}> missing required attribute '{attr}'")
    return float(val)


def _i(node: ET.Element, attr: str) -> int:
    val = node.get(attr)
    if val is None:
        raise ValueError(f"<{node.tag}> missing required attribute '{attr}'")
    return int(val)


def _s(node: ET.Element, attr: str) -> str:
    val = node.get(attr)
    if val is None or val == "":
        raise ValueError(f"<{node.tag}> missing required attribute '{attr}'")
    return val


def convert(xml_path: Path) -> dict[str, Any]:
    """Parse a legacy Space XML file and return the corresponding JSON dict."""
    tree = ET.parse(xml_path)
    space = tree.getroot()
    if space.tag != "Space":
        raise ValueError(f"Expected <Space> root, got <{space.tag}>")

    name = _s(space, "Name")

    properties_node = space.find("Properties")
    if properties_node is None:
        raise ValueError("<Space> has no <Properties> child")
    properties: list[dict[str, Any]] = []
    property_kind: dict[str, str] = {}
    for p in properties_node.findall("Property"):
        pid = _slug(_s(p, "Id"), "p")
        kind = _s(p, "PropertyType").lower()
        if kind not in ("min", "max"):
            raise ValueError(f"<Property> {pid} has invalid PropertyType={kind!r}")
        properties.append(
            {
                "id": pid,
                "name": _s(p, "Name"),
                "kind": kind,
                "weight": _i(p, "Priority"),
            }
        )
        property_kind[pid] = kind

    decisions_node = space.find("Decisions")
    if decisions_node is None:
        raise ValueError("<Space> has no <Decisions> child")
    decisions: list[dict[str, Any]] = []
    for d in decisions_node.findall("Decision"):
        decisions.append(
            {"id": _slug(_s(d, "Id"), "d"), "name": _s(d, "Name")}
        )

    alternatives_node = space.find("Alternatives")
    if alternatives_node is None:
        raise ValueError("<Space> has no <Alternatives> child")
    alternatives: list[dict[str, Any]] = []
    coefficients: list[dict[str, Any]] = []
    for a in alternatives_node.findall("Alternative"):
        aid = _slug(_s(a, "Id"), "a")
        alternatives.append(
            {
                "id": aid,
                "decisionId": _slug(_s(a, "DecisionId"), "d"),
                "name": _s(a, "Name"),
            }
        )
        coeffs_node = a.find("Coefficients")
        if coeffs_node is None:
            raise ValueError(f"<Alternative> {aid} has no <Coefficients> child")
        for c in coeffs_node.findall("Coefficient"):
            fv = c.find("FuzzyValue")
            if fv is None:
                raise ValueError(f"<Coefficient> under {aid} has no <FuzzyValue> child")
            coefficients.append(
                {
                    "alternativeId": aid,
                    "propertyId": _slug(_s(c, "PropertyId"), "p"),
                    "value": {
                        "lower": _f(fv, "O"),
                        "modal": _f(fv, "A"),
                        "upper": _f(fv, "P"),
                    },
                }
            )

    constraints: list[dict[str, Any]] = []
    constraints_node = space.find("Constraints")
    if constraints_node is not None:
        for k in constraints_node.findall("PropertyThresholdConstraint"):
            pid = _slug(_s(k, "PropertyId"), "p")
            threshold = _f(k, "Threshold")
            entry: dict[str, Any] = {"kind": "threshold", "propertyId": pid}
            kind = property_kind.get(pid)
            if kind == "min":
                entry["max"] = threshold
            elif kind == "max":
                entry["min"] = threshold
            else:
                raise ValueError(
                    f"Threshold constraint references unknown property {pid}"
                )
            constraints.append(entry)
        for k in constraints_node.findall("AlternativeDependencyConstraint"):
            constraints.append(
                {
                    "kind": "dependency",
                    "sourceAlternativeId": _slug(_s(k, "DependeeId"), "a"),
                    "targetAlternativeId": _slug(_s(k, "DependantId"), "a"),
                }
            )
        for k in constraints_node.findall("AlternativeConflictConstraint"):
            constraints.append(
                {
                    "kind": "conflict",
                    "alternativeAId": _slug(_s(k, "Alternative1Id"), "a"),
                    "alternativeBId": _slug(_s(k, "Alternative2Id"), "a"),
                }
            )

    # The legacy XML has no <Config>. Inject the runtime defaults:
    # PhiCalculationApproach.Max + SolutionApproach.Normal (1/3, 1/3, 1/3).
    config: dict[str, Any] = {
        "aggregation": "max",
        "weights": {
            "positive": 1.0 / 3.0,
            "average": 1.0 / 3.0,
            "negative": 1.0 / 3.0,
        },
    }

    return {
        "schemaVersion": "1.0.0",
        "name": name,
        "description": (
            f"Imported from legacy XML ({xml_path.name}) via "
            f"tools/import-legacy-xml.py."
        ),
        "decisions": decisions,
        "alternatives": alternatives,
        "properties": properties,
        "coefficients": coefficients,
        "constraints": constraints,
        "config": config,
    }


def main() -> int:
    parser = argparse.ArgumentParser(prog="import-legacy-xml")
    parser.add_argument("input", help="Path to a legacy Space XML file.")
    parser.add_argument(
        "-o", "--output", required=True,
        help="Path to write the converted JSON scenario.",
    )
    args = parser.parse_args()

    xml_path = Path(args.input)
    out_path = Path(args.output)
    if not xml_path.is_file():
        print(f"error: input not found: {xml_path}", file=sys.stderr)
        return 1
    try:
        scenario = convert(xml_path)
    except (ValueError, ET.ParseError) as e:
        print(f"error: conversion failed: {e}", file=sys.stderr)
        return 2

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(scenario, indent=2) + "\n")
    print(
        f"wrote {out_path} — "
        f"{len(scenario['decisions'])} decisions, "
        f"{len(scenario['alternatives'])} alternatives, "
        f"{len(scenario['properties'])} properties, "
        f"{len(scenario['coefficients'])} coefficients, "
        f"{len(scenario['constraints'])} constraints"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
