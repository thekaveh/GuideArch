"""ScenarioVM — root ViewModel for a GuideArch scenario.

Per spec/viewmodels.md §3:
  - Observable properties: scenario, file_path, is_dirty, candidates,
    critical_decisions, critical_constraints, status, warnings.
  - Commands: new_cmd, open_cmd (path), save_cmd, save_as_cmd (path), solve_cmd.
  - Re-solve semantics: any mutation to scenario fields that affect ranking
    causes solve_cmd to run.  Name/description changes do NOT trigger a solve.

Implementation notes
--------------------
This module does NOT use VMx's ComponentVMOf as its direct base because the
state here is heterogeneous (not a single M model type).  Instead it is a
plain Python class that owns its own reactivex Subject for property-change
notifications (mirroring ComponentVMOf's property_changed pattern) and wires
child VMs to the same MessageHub.

The re-solve trigger list per spec §3.3:
  - decisions, alternatives, properties, coefficients, constraints, config

The excluded trigger list (spec §3.3):
  - name, description, file_path

Debouncing (100 ms) belongs in the View adapter, not here.  Tests call
solve_cmd.execute() directly.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import reactivex as rx
from reactivex.subject import Subject
from vmx.commands.relay_command import RelayCommand, RelayCommandOfT
from vmx.messages.property_changed import PropertyChangedMessage
from vmx.messages.protocols import Message
from vmx.services.dispatcher import RxDispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.candidate import CandidateM
from guidearch.models.critical_constraint import CriticalConstraintM
from guidearch.models.critical_decision import CriticalDecisionM
from guidearch.models.scenario import ScenarioM
from guidearch.models.scenario_loader import load_scenario
from guidearch.models.topsis.critical_constraints import critical_constraints
from guidearch.models.topsis.critical_decisions import critical_decisions
from guidearch.models.topsis.solve import solve


class ScenarioVM:
    """Root ViewModel owning the loaded scenario and coordinating re-solve.

    Construction
    ------------
    Use ``make_scenario_vm()`` factory function (returns this class directly).
    The VM starts in "no scenario loaded" state.  All result lists are empty
    and ``status`` is "No scenario loaded."

    Re-solve trigger
    ----------------
    ScenarioVM subscribes to its own MessageHub.  Any child VM that mutates a
    solve-affecting field will emit a PropertyChangedMessage; ScenarioVM's
    hub subscriber calls ``_mark_solve_needed()`` which flags the next
    explicit ``solve_cmd.execute()`` call.  In the adapter layer a 100 ms
    debounce fires ``solve_cmd.execute()`` automatically.
    """

    def __init__(self) -> None:
        self._hub: MessageHub[Message] = MessageHub()
        self._dispatcher = RxDispatcher.immediate()

        # ── State ────────────────────────────────────────────────────────────
        self._scenario: ScenarioM | None = None
        self._file_path: str | None = None
        self._is_dirty: bool = False
        self._candidates: tuple[CandidateM, ...] = ()
        self._critical_decisions: tuple[CriticalDecisionM, ...] = ()
        self._critical_constraints: tuple[CriticalConstraintM, ...] = ()
        self._status: str = "No scenario loaded."
        self._warnings: tuple[str, ...] = ()

        # ── property_changed subject (INPC-equivalent) ───────────────────────
        self._property_changed_subject: Subject[str] = Subject()

        # ── Hub subscription — detect child mutations ────────────────────────
        self._solve_needed: bool = False
        self._hub_sub = self._hub.messages.subscribe(
            on_next=self._on_hub_message
        )

        # ── Commands ─────────────────────────────────────────────────────────
        # solve_cmd trigger: property_changed observable fires when scenario
        # is set/cleared, enabling/disabling the command.
        _solve_trigger: rx.Observable[object] = self._property_changed_subject

        self.solve_cmd: RelayCommand = (
            RelayCommand.builder()
            .predicate(lambda: self._scenario is not None)
            .task(self._do_solve)
            .triggers(_solve_trigger)
            .build()
        )

        self.new_cmd: RelayCommand = (
            RelayCommand.builder()
            .task(self._do_new)
            .build()
        )

        # open_cmd accepts a path parameter (string)
        self.open_cmd: RelayCommandOfT[str] = (
            RelayCommandOfT.builder()
            .task(self._do_open)
            .build()
        )

        # save_cmd — disabled when no file_path
        self.save_cmd: RelayCommand = (
            RelayCommand.builder()
            .predicate(lambda: self._file_path is not None and self._scenario is not None)
            .task(self._do_save)
            .triggers(_solve_trigger)
            .build()
        )

        # save_as_cmd accepts a path parameter (string)
        self.save_as_cmd: RelayCommandOfT[str] = (
            RelayCommandOfT.builder()
            .task(self._do_save_as)
            .build()
        )

    # ── Observable properties ────────────────────────────────────────────────

    @property
    def property_changed(self) -> rx.Observable[str]:
        """Observable that emits property names when they change."""
        return self._property_changed_subject

    @property
    def scenario(self) -> ScenarioM | None:
        return self._scenario

    @property
    def file_path(self) -> str | None:
        return self._file_path

    @property
    def is_dirty(self) -> bool:
        return self._is_dirty

    @property
    def candidates(self) -> tuple[CandidateM, ...]:
        return self._candidates

    @property
    def critical_decisions_result(self) -> tuple[CriticalDecisionM, ...]:
        return self._critical_decisions

    @property
    def critical_constraints_result(self) -> tuple[CriticalConstraintM, ...]:
        return self._critical_constraints

    @property
    def status(self) -> str:
        return self._status

    @property
    def warnings(self) -> tuple[str, ...]:
        return self._warnings

    @property
    def hub(self) -> MessageHub[Message]:
        """The shared MessageHub for all child VMs."""
        return self._hub

    # ── Command implementations ──────────────────────────────────────────────

    def _do_new(self) -> None:
        """Replace scenario with an empty one.  Clears file path and dirty flag."""
        from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
        from guidearch.models.scenario import ConfigM

        empty = ScenarioM(
            schema_version="1.0.0",
            name="New Scenario",
            description="",
            decisions=(),
            alternatives=(),
            properties=(),
            coefficients=(),
            constraints=(),
            config=ConfigM(
                aggregation="max",
                weights=NormalizedFuzzyM(positive=0.5, average=0.0, negative=0.5),
            ),
        )
        self._scenario = empty
        self._file_path = None
        self._is_dirty = False
        self._candidates = ()
        self._critical_decisions = ()
        self._critical_constraints = ()
        self._status = "New scenario created."
        self._warnings = ()
        self._raise_property_changed("scenario")
        self._raise_property_changed("file_path")
        self._raise_property_changed("is_dirty")
        self._raise_property_changed("candidates")
        self._raise_property_changed("critical_decisions_result")
        self._raise_property_changed("critical_constraints_result")
        self._raise_property_changed("status")
        self._raise_property_changed("warnings")

    def _do_open(self, path: str | None) -> None:
        """Load scenario from *path*.  On failure, emit warning without mutating state."""
        if not path:
            return
        try:
            scenario = load_scenario(Path(path))
        except Exception as exc:
            # Emit warning, do not mutate state
            new_warnings = (*self._warnings, f"Failed to load '{path}': {exc}")
            self._warnings = new_warnings
            self._raise_property_changed("warnings")
            return

        self._scenario = scenario
        self._file_path = path
        self._is_dirty = False
        self._warnings = scenario.warnings
        self._candidates = ()
        self._critical_decisions = ()
        self._critical_constraints = ()
        self._status = f"Loaded: {scenario.name}"
        self._raise_property_changed("scenario")
        self._raise_property_changed("file_path")
        self._raise_property_changed("is_dirty")
        self._raise_property_changed("warnings")
        self._raise_property_changed("candidates")
        self._raise_property_changed("critical_decisions_result")
        self._raise_property_changed("critical_constraints_result")
        self._raise_property_changed("status")

        # Auto-solve on open
        self._do_solve()

    def _do_save(self) -> None:
        """Write current scenario to file_path as JSON."""
        if self._scenario is None or self._file_path is None:
            return
        self._write_scenario(self._scenario, self._file_path)
        self._is_dirty = False
        self._raise_property_changed("is_dirty")

    def _do_save_as(self, path: str | None) -> None:
        """Set file_path = path then save."""
        if not path:
            return
        self._file_path = path
        self._raise_property_changed("file_path")
        self._do_save()

    def _do_solve(self) -> None:
        """Re-run solve + analyses; update candidates, criticals, status."""
        if self._scenario is None:
            return
        scenario = self._scenario
        try:
            self._candidates = solve(scenario)
            self._critical_decisions = critical_decisions(scenario, self._candidates)
            self._critical_constraints = critical_constraints(scenario)
            self._status = (
                f"{scenario.name}: {len(self._candidates)} candidate(s)"
            )
        except Exception as exc:
            self._status = f"Solve error: {exc}"
            self._candidates = ()
            self._critical_decisions = ()
            self._critical_constraints = ()
        self._solve_needed = False
        self._raise_property_changed("candidates")
        self._raise_property_changed("critical_decisions_result")
        self._raise_property_changed("critical_constraints_result")
        self._raise_property_changed("status")

    # ── Solve trigger via hub ────────────────────────────────────────────────

    def _on_hub_message(self, message: Message) -> None:
        """React to property changes from child VMs.

        Sets _solve_needed flag; actual re-solve is either done immediately
        (when called from tests via solve_cmd.execute()) or debounced in the
        adapter layer.
        """
        if isinstance(message, PropertyChangedMessage) and message.property_name == "model":
            self._solve_needed = True
            self._is_dirty = True
            self._raise_property_changed("is_dirty")

    def _mark_solve_needed(self) -> None:
        """Explicitly flag that a solve is needed (called by adapters)."""
        self._solve_needed = True

    # ── Internal helpers ─────────────────────────────────────────────────────

    def _raise_property_changed(self, property_name: str) -> None:
        self._property_changed_subject.on_next(property_name)
        # Also broadcast on hub for cross-VM subscribers
        self._hub.send(
            PropertyChangedMessage.create(self, "scenario-vm", property_name)
        )

    @staticmethod
    def _write_scenario(scenario: ScenarioM, path: str) -> None:
        """Serialise ScenarioM to JSON at *path* (sorted keys, UTF-8)."""
        data = _scenario_to_dict(scenario)
        Path(path).write_text(
            json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False),
            encoding="utf-8",
        )

    def dispose(self) -> None:
        """Clean up subscriptions and subjects."""
        self._hub_sub.dispose()
        self.solve_cmd.dispose()
        self.new_cmd.dispose()
        self.open_cmd.dispose()
        self.save_cmd.dispose()
        self.save_as_cmd.dispose()
        self._property_changed_subject.on_completed()
        self._property_changed_subject.dispose()
        self._hub.dispose()


# ---------------------------------------------------------------------------
# Serialisation helper (mirrors scenario_loader in reverse)
# ---------------------------------------------------------------------------


def _scenario_to_dict(scenario: ScenarioM) -> dict[str, Any]:
    return {
        "schemaVersion": scenario.schema_version,
        "name": scenario.name,
        "description": scenario.description,
        "decisions": [{"id": d.id, "name": d.name} for d in scenario.decisions],
        "alternatives": [
            {"id": a.id, "decisionId": a.decision_id, "name": a.name}
            for a in scenario.alternatives
        ],
        "properties": [
            {"id": p.id, "name": p.name, "kind": p.kind, "weight": p.weight}
            for p in scenario.properties
        ],
        "coefficients": [
            {
                "alternativeId": c.alternative_id,
                "propertyId": c.property_id,
                "value": {
                    "lower": c.value.lower,
                    "modal": c.value.modal,
                    "upper": c.value.upper,
                },
            }
            for c in scenario.coefficients
        ],
        "constraints": [_constraint_to_dict(c) for c in scenario.constraints],
        "config": {
            "aggregation": scenario.config.aggregation,
            "weights": {
                "positive": scenario.config.weights.positive,
                "average": scenario.config.weights.average,
                "negative": scenario.config.weights.negative,
            },
        },
    }


def _constraint_to_dict(c: Any) -> dict[str, Any]:
    from guidearch.models.constraint import (
        ConflictConstraint,
        DependencyConstraint,
        ThresholdConstraint,
    )

    if isinstance(c, ThresholdConstraint):
        result: dict[str, Any] = {"kind": "threshold", "propertyId": c.property_id}
        if c.min is not None:
            result["min"] = c.min
        if c.max is not None:
            result["max"] = c.max
        return result
    elif isinstance(c, DependencyConstraint):
        return {
            "kind": "dependency",
            "sourceAlternativeId": c.source_alternative_id,
            "targetAlternativeId": c.target_alternative_id,
        }
    else:
        assert isinstance(c, ConflictConstraint)
        return {
            "kind": "conflict",
            "alternativeAId": c.alternative_a_id,
            "alternativeBId": c.alternative_b_id,
        }


# ---------------------------------------------------------------------------
# Factory function
# ---------------------------------------------------------------------------


def make_scenario_vm() -> ScenarioVM:
    """Create and return a new ScenarioVM in the initial (no-scenario) state."""
    return ScenarioVM()
