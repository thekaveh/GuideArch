"""GuideArch ViewModel layer — public re-exports.

The full VM tree per spec/viewmodels.md §2:

  AppVM
  └── ScenarioVM
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
from guidearch.viewmodels.app_vm import (
    DEFAULT_THEME,
    AppVM,
    Mode,
    known_themes,
    make_app_vm,
    register_theme,
)
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
from guidearch.viewmodels.scenario_vm import (
    ScenarioMutationError,
    ScenarioVM,
    make_scenario_vm,
)

__all__ = [
    "DEFAULT_THEME",
    "AlternativeVM",
    "AppVM",
    "CandidateVM",
    "CoefficientVM",
    "ConflictConstraintVM",
    "CriticalConstraintVM",
    "CriticalDecisionVM",
    "DecisionVM",
    "DependencyConstraintVM",
    "Mode",
    "PropertyVM",
    "ScenarioMutationError",
    "ScenarioVM",
    "ThresholdConstraintVM",
    "known_themes",
    "make_app_vm",
    "make_scenario_vm",
    "register_theme",
]
