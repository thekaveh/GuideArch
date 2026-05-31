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

Cascade methods (M3)
--------------------
delete_decision(id), delete_alternative(id), delete_property(id) implement
the cascade semantics from spec/editors.md §2:
  - delete_decision: removes decision, its alternatives, their coefficients,
    and any constraints referencing those alternatives.
  - delete_alternative: removes the alternative, its coefficients, and
    any constraints referencing it.
  - delete_property: removes the property, its coefficients, and any
    threshold constraints referencing it.
All cascades call _apply_scenario_mutation() which marks dirty and re-solves.

Add methods (M3)
----------------
add_decision(), add_alternative(decision_id), add_property() append new
entities with zero-fuzzy coefficients for every pairing.

Update methods (M3)
-------------------
update_decision_name(id, name), update_alternative_name(id, name),
update_property(id, name, kind, weight), update_coefficient(alt_id, prop_id,
lower, modal, upper), update_constraint_threshold(...),
update_constraint_dependency(...), update_constraint_conflict(...)
allow in-place mutation with automatic dirty marking and re-solve.
"""

from __future__ import annotations

import json
import uuid
from dataclasses import replace
from pathlib import Path
from typing import Any, Literal

import reactivex as rx
from reactivex.subject import Subject
from vmx.commands.relay_command import RelayCommand, RelayCommandOfT
from vmx.messages.property_changed import PropertyChangedMessage
from vmx.messages.protocols import Message
from vmx.services.dispatcher import RxDispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.alternative import AlternativeM
from guidearch.models.candidate import CandidateM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.constraint import (
    ConflictConstraint,
    Constraint,
    DependencyConstraint,
    ThresholdConstraint,
)
from guidearch.models.critical_constraint import CriticalConstraintM
from guidearch.models.critical_decision import CriticalDecisionM
from guidearch.models.decision import DecisionM
from guidearch.models.property import PropertyM
from guidearch.models.scenario import ScenarioM
from guidearch.models.scenario_loader import load_scenario
from guidearch.models.topsis.critical_constraints import critical_constraints
from guidearch.models.topsis.critical_decisions import critical_decisions
from guidearch.models.topsis.solve import solve
from guidearch.models.triangular_fuzzy import TriangularFuzzyM


class _UNSET:
    """Sentinel for 'argument not provided' (distinguishes None from missing)."""


class ScenarioMutationError(ValueError):
    """Raised when a ScenarioVM mutation call is invalid: no scenario loaded,
    referenced entity ID not found, constraint index out of range, or the
    constraint at the given index is not of the expected kind.

    Subclasses ValueError so existing `except ValueError` callers keep working;
    new callers can catch ScenarioMutationError specifically to distinguish
    VM-misuse errors from other ValueErrors raised by the surrounding code.
    """


class ScenarioVM:
    """Root ViewModel owning the loaded scenario and coordinating re-solve.

    Construction
    ------------
    Use ``make_scenario_vm()`` factory function (returns this class directly).
    The VM starts in "no scenario loaded" state.  All result lists are empty
    and ``status`` is "No scenario loaded."

    Re-solve trigger
    ----------------
    ScenarioVM subscribes to its own MessageHub. Any child VM that mutates a
    solve-affecting field emits a PropertyChangedMessage; the hub subscriber
    sets ``_solve_needed`` directly. v1.0 re-solves synchronously in
    ``_apply_scenario_mutation``; debounced solve is deferred to v1.1
    (see spec/editors.md §0).
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
        self._selected_candidate_index: int | None = None

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
    def selected_candidate_index(self) -> int | None:
        return self._selected_candidate_index

    @selected_candidate_index.setter
    def selected_candidate_index(self, value: int | None) -> None:
        self._selected_candidate_index = value
        self._raise_property_changed("selected_candidate_index")

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
            name="New scenario",
            description="",
            decisions=(),
            alternatives=(),
            properties=(),
            coefficients=(),
            constraints=(),
            config=ConfigM(
                aggregation="max",
                weights=NormalizedFuzzyM(
                    positive=1.0 / 3.0,
                    average=1.0 / 3.0,
                    negative=1.0 / 3.0,
                ),
            ),
        )
        self._scenario = empty
        self._file_path = None
        self._is_dirty = False
        self._candidates = ()
        self._critical_decisions = ()
        self._critical_constraints = ()
        self._status = "New scenario — nothing to solve."
        self._warnings = ()
        self._selected_candidate_index = None
        self._raise_property_changed("scenario")
        self._raise_property_changed("file_path")
        self._raise_property_changed("is_dirty")
        self._raise_property_changed("candidates")
        self._raise_property_changed("selected_candidate_index")
        self._raise_property_changed("critical_decisions_result")
        self._raise_property_changed("critical_constraints_result")
        self._raise_property_changed("status")
        self._raise_property_changed("warnings")

    def _do_open(self, path: str | None) -> None:
        """Load scenario from *path*.

        On failure, emit a warning AND update status — but do not mutate the
        scenario / file_path / dirty flag. Matches the TS+C# behavior so the
        status bar surfaces the error consistently across impls (spec §3.2
        says "do not mutate state; emit warning" — read as "do not mutate
        scenario state"; status is a display-only signal).
        """
        if not path:
            return
        try:
            scenario = load_scenario(Path(path))
        except Exception as exc:
            msg = f"Open failed: {exc}"
            self._status = msg
            self._warnings = (*self._warnings, msg)
            self._raise_property_changed("status")
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
        self._selected_candidate_index = None
        self._raise_property_changed("scenario")
        self._raise_property_changed("file_path")
        self._raise_property_changed("is_dirty")
        self._raise_property_changed("warnings")
        self._raise_property_changed("candidates")
        self._raise_property_changed("selected_candidate_index")
        self._raise_property_changed("critical_decisions_result")
        self._raise_property_changed("critical_constraints_result")
        self._raise_property_changed("status")

        # Auto-solve on open
        self._do_solve()

    def _do_save(self) -> None:
        """Write current scenario to file_path as JSON.

        On IO failure (disk full, permission denied, removable media gone)
        catch and surface to the status line + warnings list instead of
        letting the exception bubble to the NiceGUI handler (which would
        leave the UI in an inconsistent state with no user-visible message).
        Matches the TS/C# defensive Save behavior.
        """
        if self._scenario is None or self._file_path is None:
            return
        try:
            self._write_scenario(self._scenario, self._file_path)
        except OSError as exc:
            msg = f"Save failed: {exc}"
            self._status = msg
            self._warnings = (*self._warnings, msg)
            self._raise_property_changed("status")
            self._raise_property_changed("warnings")
            return
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
            self._status = f"Solved: {len(self._candidates)} candidates"
        except Exception as exc:
            self._status = f"Solve error: {exc}"
            self._candidates = ()
            self._critical_decisions = ()
            self._critical_constraints = ()
        # Default selection to rank-0 when candidates become non-empty
        if self._candidates:
            if self._selected_candidate_index is None:
                self._selected_candidate_index = 0
        else:
            self._selected_candidate_index = None
        self._solve_needed = False
        self._raise_property_changed("candidates")
        self._raise_property_changed("selected_candidate_index")
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

    # ── M3 cascade / mutation helpers ────────────────────────────────────────

    def _apply_scenario_mutation(self, new_scenario: ScenarioM) -> None:
        """Replace scenario, mark dirty, broadcast change, trigger re-solve."""
        self._scenario = new_scenario
        self._is_dirty = True
        self._raise_property_changed("scenario")
        self._raise_property_changed("is_dirty")
        self._do_solve()

    # ── Delete cascades ───────────────────────────────────────────────────────

    def delete_decision(self, decision_id: str) -> None:
        """Delete a decision and cascade: removes its alternatives, their
        coefficients, and any constraints referencing those alternatives.

        Raises ValueError if no scenario is loaded or decision_id not found.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        # Verify decision exists
        if not any(d.id == decision_id for d in scenario.decisions):
            raise ScenarioMutationError(f"Decision '{decision_id}' not found.")

        # Collect affected alternative ids
        affected_alt_ids = {a.id for a in scenario.alternatives if a.decision_id == decision_id}

        # Remove decision
        new_decisions = tuple(d for d in scenario.decisions if d.id != decision_id)
        # Remove alternatives belonging to this decision
        new_alternatives = tuple(a for a in scenario.alternatives if a.id not in affected_alt_ids)
        # Remove coefficients referencing affected alternatives
        new_coefficients = tuple(
            c for c in scenario.coefficients if c.alternative_id not in affected_alt_ids
        )
        # Remove constraints referencing affected alternatives
        new_constraints = _remove_constraints_for_alternatives(
            scenario.constraints, affected_alt_ids
        )

        self._apply_scenario_mutation(
            replace(
                scenario,
                decisions=new_decisions,
                alternatives=new_alternatives,
                coefficients=new_coefficients,
                constraints=new_constraints,
            )
        )

    def delete_alternative(self, alternative_id: str) -> None:
        """Delete an alternative and cascade: removes its coefficients and any
        constraints referencing it.

        Raises ValueError if no scenario is loaded or alternative_id not found.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        if not any(a.id == alternative_id for a in scenario.alternatives):
            raise ScenarioMutationError(f"Alternative '{alternative_id}' not found.")

        affected_ids = {alternative_id}
        new_alternatives = tuple(a for a in scenario.alternatives if a.id != alternative_id)
        new_coefficients = tuple(
            c for c in scenario.coefficients if c.alternative_id != alternative_id
        )
        new_constraints = _remove_constraints_for_alternatives(
            scenario.constraints, affected_ids
        )

        self._apply_scenario_mutation(
            replace(
                scenario,
                alternatives=new_alternatives,
                coefficients=new_coefficients,
                constraints=new_constraints,
            )
        )

    def delete_property(self, property_id: str) -> None:
        """Delete a property and cascade: removes its coefficients and any
        threshold constraints referencing it.

        Raises ValueError if no scenario is loaded or property_id not found.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        if not any(p.id == property_id for p in scenario.properties):
            raise ScenarioMutationError(f"Property '{property_id}' not found.")

        new_properties = tuple(p for p in scenario.properties if p.id != property_id)
        new_coefficients = tuple(
            c for c in scenario.coefficients if c.property_id != property_id
        )
        new_constraints = tuple(
            c
            for c in scenario.constraints
            if not (isinstance(c, ThresholdConstraint) and c.property_id == property_id)
        )

        self._apply_scenario_mutation(
            replace(
                scenario,
                properties=new_properties,
                coefficients=new_coefficients,
                constraints=new_constraints,
            )
        )

    def delete_constraint(self, index: int) -> None:
        """Delete a constraint by its position in scenario.constraints.

        Raises ValueError if index is out of range.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        if index < 0 or index >= len(scenario.constraints):
            raise ScenarioMutationError(f"Constraint index {index} out of range.")

        new_constraints = tuple(c for i, c in enumerate(scenario.constraints) if i != index)
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))

    # ── Add operations ────────────────────────────────────────────────────────

    def add_decision(self, name: str = "New decision") -> str:
        """Append a new decision; returns its new id."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        new_id = f"d-{uuid.uuid4()}"
        new_decision = DecisionM(id=new_id, name=name)
        self._apply_scenario_mutation(
            replace(scenario, decisions=(*scenario.decisions, new_decision))
        )
        return new_id

    def add_alternative(self, decision_id: str, name: str = "New alternative") -> str:
        """Append a new alternative under decision_id; adds zero-fuzzy coefficients
        for every existing property.  Returns new alternative id.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        if not any(d.id == decision_id for d in scenario.decisions):
            raise ScenarioMutationError(f"Decision '{decision_id}' not found.")
        new_id = f"a-{uuid.uuid4()}"
        new_alt = AlternativeM(id=new_id, decision_id=decision_id, name=name)
        new_coefficients = tuple(
            CoefficientM(
                alternative_id=new_id,
                property_id=p.id,
                value=TriangularFuzzyM(0.0, 0.0, 0.0),
            )
            for p in scenario.properties
        )
        self._apply_scenario_mutation(
            replace(
                scenario,
                alternatives=(*scenario.alternatives, new_alt),
                coefficients=(*scenario.coefficients, *new_coefficients),
            )
        )
        return new_id

    def add_property(
        self,
        name: str = "New property",
        kind: Literal["min", "max"] = "min",
        weight: float = 1.0,
    ) -> str:
        """Append a new property; adds zero-fuzzy coefficients for every
        existing alternative.  Returns new property id.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        new_id = f"p-{uuid.uuid4()}"
        new_prop = PropertyM(id=new_id, name=name, kind=kind, weight=weight)
        new_coefficients = tuple(
            CoefficientM(
                alternative_id=a.id,
                property_id=new_id,
                value=TriangularFuzzyM(0.0, 0.0, 0.0),
            )
            for a in scenario.alternatives
        )
        self._apply_scenario_mutation(
            replace(
                scenario,
                properties=(*scenario.properties, new_prop),
                coefficients=(*scenario.coefficients, *new_coefficients),
            )
        )
        return new_id

    def add_threshold_constraint(
        self,
        property_id: str,
        min_val: float | None = None,
        max_val: float | None = None,
    ) -> int:
        """Add a threshold constraint; returns its index.

        Enforces three things the schema and invariants require at load time
        but the previous implementation silently violated at mutation time:
          - property_id must reference an existing Property (invariant 2.5
            counterpart for thresholds).
          - At least one of min/max must be set (schema $defs/Constraint:
            anyOf [{required:[min]}, {required:[max]}]).
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        if not any(p.id == property_id for p in scenario.properties):
            raise ScenarioMutationError(f"Property '{property_id}' not found.")
        if min_val is None and max_val is None:
            raise ScenarioMutationError(
                "ThresholdConstraint requires at least one of min or max."
            )
        new_c: Constraint = ThresholdConstraint(
            kind="threshold", property_id=property_id, min=min_val, max=max_val
        )
        new_constraints = (*scenario.constraints, new_c)
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))
        return len(new_constraints) - 1

    def add_dependency_constraint(self, source_id: str, target_id: str) -> int:
        """Add a dependency constraint; returns its index.

        Enforces invariants 2.5 (referenced alternatives must exist) and
        7.1 (self-edges are fatal).
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        alt_ids = {a.id for a in scenario.alternatives}
        if source_id not in alt_ids:
            raise ScenarioMutationError(f"Alternative '{source_id}' not found.")
        if target_id not in alt_ids:
            raise ScenarioMutationError(f"Alternative '{target_id}' not found.")
        if source_id == target_id:
            raise ScenarioMutationError(
                "Self-edge on dependency constraint (source must differ from target)."
            )
        new_c: Constraint = DependencyConstraint(
            kind="dependency",
            source_alternative_id=source_id,
            target_alternative_id=target_id,
        )
        new_constraints = (*scenario.constraints, new_c)
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))
        return len(new_constraints) - 1

    def add_conflict_constraint(self, alt_a_id: str, alt_b_id: str) -> int:
        """Add a conflict constraint; returns its index.

        Enforces invariants 2.5 and 7.1 (see add_dependency_constraint).
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        alt_ids = {a.id for a in scenario.alternatives}
        if alt_a_id not in alt_ids:
            raise ScenarioMutationError(f"Alternative '{alt_a_id}' not found.")
        if alt_b_id not in alt_ids:
            raise ScenarioMutationError(f"Alternative '{alt_b_id}' not found.")
        if alt_a_id == alt_b_id:
            raise ScenarioMutationError(
                "Self-edge on conflict constraint (alternativeA must differ from alternativeB)."
            )
        new_c: Constraint = ConflictConstraint(
            kind="conflict",
            alternative_a_id=alt_a_id,
            alternative_b_id=alt_b_id,
        )
        new_constraints = (*scenario.constraints, new_c)
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))
        return len(new_constraints) - 1

    # ── Update operations ─────────────────────────────────────────────────────

    def update_decision_name(self, decision_id: str, name: str) -> None:
        """Rename a decision (does not trigger a solve)."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        new_decisions = tuple(
            replace(d, name=name) if d.id == decision_id else d
            for d in scenario.decisions
        )
        # Name change does not trigger solve — use a lighter update
        self._scenario = replace(scenario, decisions=new_decisions)
        self._is_dirty = True
        self._raise_property_changed("scenario")
        self._raise_property_changed("is_dirty")

    def update_alternative_name(self, alternative_id: str, name: str) -> None:
        """Rename an alternative (does not trigger a solve)."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        new_alts = tuple(
            replace(a, name=name) if a.id == alternative_id else a
            for a in scenario.alternatives
        )
        self._scenario = replace(scenario, alternatives=new_alts)
        self._is_dirty = True
        self._raise_property_changed("scenario")
        self._raise_property_changed("is_dirty")

    def update_property(
        self,
        property_id: str,
        name: str | None = None,
        kind: Literal["min", "max"] | None = None,
        weight: float | None = None,
    ) -> None:
        """Update one or more fields of a property.  Kind/weight changes trigger
        a re-solve; name-only change does not.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        if weight is not None and weight <= 0:
            # Schema $defs/Property.weight is exclusiveMinimum 0 — match at the
            # mutation boundary instead of letting it surface at save-time only.
            raise ScenarioMutationError(
                f"Property weight must be > 0 (got {weight})."
            )
        scenario = self._scenario
        if not any(p.id == property_id for p in scenario.properties):
            raise ScenarioMutationError(f"Property '{property_id}' not found.")
        triggers_solve = kind is not None or weight is not None
        new_props: list[PropertyM] = []
        for p in scenario.properties:
            if p.id == property_id:
                new_props.append(
                    replace(
                        p,
                        name=name if name is not None else p.name,
                        kind=kind if kind is not None else p.kind,
                        weight=weight if weight is not None else p.weight,
                    )
                )
            else:
                new_props.append(p)
        new_scenario = replace(scenario, properties=tuple(new_props))
        if triggers_solve:
            self._apply_scenario_mutation(new_scenario)
        else:
            self._scenario = new_scenario
            self._is_dirty = True
            self._raise_property_changed("scenario")
            self._raise_property_changed("is_dirty")

    def update_coefficient(
        self,
        alternative_id: str,
        property_id: str,
        lower: float,
        modal: float,
        upper: float,
    ) -> None:
        """Update a coefficient value; triggers a re-solve.

        Emits a warning (not a fatal error) if triangular ordering
        (lower <= modal <= upper) is violated — matches C# and TS behavior
        and is consistent with invariant 4.1 which the loader also warns on.
        Stale ordering warnings are dropped when the cell is edited back
        into shape so the warning chip clears.
        """
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        new_value = TriangularFuzzyM(lower=lower, modal=modal, upper=upper)
        new_coefficients = tuple(
            replace(c, value=new_value)
            if c.alternative_id == alternative_id and c.property_id == property_id
            else c
            for c in scenario.coefficients
        )
        warn_msg = (
            f"Coefficient ({alternative_id}, {property_id}): "
            f"ordering violated lower={lower} modal={modal} upper={upper}"
        )
        # Always drop the previous ordering warning for this (alt, prop) pair —
        # we re-add it below only if the new value still violates.
        stale_prefix = f"Coefficient ({alternative_id}, {property_id}): ordering"
        new_warnings = tuple(w for w in self._warnings if not w.startswith(stale_prefix))
        if lower > modal or modal > upper:
            new_warnings = (*new_warnings, warn_msg)
        if new_warnings != self._warnings:
            self._warnings = new_warnings
            self._raise_property_changed("warnings")
        self._apply_scenario_mutation(replace(scenario, coefficients=new_coefficients))

    def update_threshold_constraint(
        self,
        index: int,
        property_id: str | None = None,
        min_val: float | None | type[_UNSET] = _UNSET,
        max_val: float | None | type[_UNSET] = _UNSET,
    ) -> None:
        """Update a threshold constraint at the given index."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        c = scenario.constraints[index]
        if not isinstance(c, ThresholdConstraint):
            raise ScenarioMutationError(
                f"Constraint at index {index} is not a ThresholdConstraint."
            )
        new_c = ThresholdConstraint(
            kind="threshold",
            property_id=property_id if property_id is not None else c.property_id,
            min=c.min if min_val is _UNSET else min_val,  # type: ignore[arg-type]
            max=c.max if max_val is _UNSET else max_val,  # type: ignore[arg-type]
        )
        new_constraints = tuple(
            new_c if i == index else old_c
            for i, old_c in enumerate(scenario.constraints)
        )
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))

    def update_dependency_constraint(
        self,
        index: int,
        source_id: str | None = None,
        target_id: str | None = None,
    ) -> None:
        """Update a dependency constraint at the given index."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        c = scenario.constraints[index]
        if not isinstance(c, DependencyConstraint):
            raise ScenarioMutationError(
                f"Constraint at index {index} is not a DependencyConstraint."
            )
        new_c = DependencyConstraint(
            kind="dependency",
            source_alternative_id=(
                source_id if source_id is not None else c.source_alternative_id
            ),
            target_alternative_id=(
                target_id if target_id is not None else c.target_alternative_id
            ),
        )
        new_constraints = tuple(
            new_c if i == index else old_c
            for i, old_c in enumerate(scenario.constraints)
        )
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))

    def update_conflict_constraint(
        self,
        index: int,
        alt_a_id: str | None = None,
        alt_b_id: str | None = None,
    ) -> None:
        """Update a conflict constraint at the given index."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        scenario = self._scenario
        c = scenario.constraints[index]
        if not isinstance(c, ConflictConstraint):
            raise ScenarioMutationError(f"Constraint at index {index} is not a ConflictConstraint.")
        new_c = ConflictConstraint(
            kind="conflict",
            alternative_a_id=(
                alt_a_id if alt_a_id is not None else c.alternative_a_id
            ),
            alternative_b_id=(
                alt_b_id if alt_b_id is not None else c.alternative_b_id
            ),
        )
        new_constraints = tuple(
            new_c if i == index else old_c
            for i, old_c in enumerate(scenario.constraints)
        )
        self._apply_scenario_mutation(replace(scenario, constraints=new_constraints))

    def update_scenario_name(self, name: str) -> None:
        """Update scenario name (does not trigger re-solve)."""
        if self._scenario is None:
            raise ScenarioMutationError("No scenario loaded.")
        self._scenario = replace(self._scenario, name=name)
        self._is_dirty = True
        self._raise_property_changed("scenario")
        self._raise_property_changed("is_dirty")

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
# Cascade helper
# ---------------------------------------------------------------------------


def _remove_constraints_for_alternatives(
    constraints: tuple[Constraint, ...],
    affected_alt_ids: set[str],
) -> tuple[Constraint, ...]:
    """Return constraints with any entry referencing affected_alt_ids removed."""
    result: list[Constraint] = []
    for c in constraints:
        if isinstance(c, DependencyConstraint):
            if (
                c.source_alternative_id in affected_alt_ids
                or c.target_alternative_id in affected_alt_ids
            ):
                continue
        elif isinstance(c, ConflictConstraint) and (
            c.alternative_a_id in affected_alt_ids or c.alternative_b_id in affected_alt_ids
        ):
            continue
        result.append(c)
    return tuple(result)


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
