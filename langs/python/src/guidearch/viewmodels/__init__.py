"""GuideArch ViewModel layer — public re-exports.

The full VM tree per spec/viewmodels.md §2:

  ScenarioVM
  ├── DecisionVM          (ComponentVMOf[DecisionM])
  │   └── AlternativeVM   (ComponentVMOf[AlternativeM])
  ├── PropertyVM          (ComponentVMOf[PropertyM])
  ├── CoefficientVM       (ComponentVMOf[CoefficientM])
  ├── ThresholdConstraintVM / DependencyConstraintVM / ConflictConstraintVM
  ├── CandidateVM         (read-only result wrapper)
  ├── CriticalDecisionVM  (read-only result wrapper)
  └── CriticalConstraintVM (read-only result wrapper)
"""

from guidearch.viewmodels.alternative_vm import AlternativeVM
from guidearch.viewmodels.candidate_vm import CandidateVM
from guidearch.viewmodels.coefficient_vm import CoefficientVM
from guidearch.viewmodels.constraint_vm import (
    ConflictConstraintVM,
    DependencyConstraintVM,
    ThresholdConstraintVM,
)
from guidearch.viewmodels.critical_constraint_vm import CriticalConstraintVM
from guidearch.viewmodels.critical_decision_vm import CriticalDecisionVM
from guidearch.viewmodels.decision_vm import DecisionVM
from guidearch.viewmodels.property_vm import PropertyVM
from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

__all__ = [
    "AlternativeVM",
    "CandidateVM",
    "CoefficientVM",
    "ConflictConstraintVM",
    "CriticalConstraintVM",
    "CriticalDecisionVM",
    "DecisionVM",
    "DependencyConstraintVM",
    "PropertyVM",
    "ScenarioVM",
    "ThresholdConstraintVM",
    "make_scenario_vm",
]
