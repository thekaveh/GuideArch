"""VMx 3.1 command compatibility tests."""

from __future__ import annotations

from vmx.commands.relay_command import RelayCommandOf

from guidearch.viewmodels.app_vm import make_app_vm
from guidearch.viewmodels.scenario_vm import make_scenario_vm


def test_python_viewmodels_use_vmx31_parameterized_command_name() -> None:
    app = make_app_vm(load_theme=lambda: "dark", persist_theme=lambda _: None, mode="web")
    scenario = make_scenario_vm()

    assert isinstance(app.set_theme_cmd, RelayCommandOf)
    assert isinstance(scenario.open_cmd, RelayCommandOf)
    assert isinstance(scenario.save_as_cmd, RelayCommandOf)
