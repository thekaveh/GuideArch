"""Unit tests for the VMx → NiceGUI adapter against stub UI elements.

Tests cover:
- bind(): VM → stub propagates value on property change.
- bind(): UI → VM write-back via proxy.
- bind(): read-only properties don't raise on UI write attempt.
- bind_command(): click invokes cmd.execute().
- bind_command(): disabled state follows can_execute.
"""

from __future__ import annotations

from typing import Any

import reactivex as rx
from reactivex.subject import Subject

from guidearch.view.adapters.vmx_to_nicegui import _Proxy, bind, bind_command

# ---------------------------------------------------------------------------
# Stub implementations
# ---------------------------------------------------------------------------


class _StubVM:
    """Minimal VM stub with a property_changed observable."""

    def __init__(self, initial_value: Any = "hello") -> None:
        self._value = initial_value
        self._subject: Subject[str] = Subject()

    @property
    def property_changed(self) -> rx.Observable[str]:
        return self._subject

    @property
    def my_prop(self) -> Any:
        return self._value

    @my_prop.setter
    def my_prop(self, v: Any) -> None:
        self._value = v

    def emit_change(self, prop_name: str) -> None:
        self._subject.on_next(prop_name)

    def dispose(self) -> None:
        self._subject.on_completed()
        self._subject.dispose()


class _ReadonlyVM(_StubVM):
    """VM with a read-only property (no setter)."""

    @property  # type: ignore[misc]
    def my_prop(self) -> Any:
        return self._value


class _StubElement:
    """Minimal NiceGUI element stub."""

    def __init__(self, initial_value: Any = None) -> None:
        self.value = initial_value
        self._bound_obj: Any = None
        self._bound_attr: str | None = None
        self._on_change_cb: Any = None

    def set_value(self, v: Any) -> None:
        self.value = v

    def bind_value(self, obj: Any, attr: str) -> None:
        self._bound_obj = obj
        self._bound_attr = attr

    def on(self, event: str, callback: Any) -> None:
        """Simulate NiceGUI's .on() event registration."""
        if event == "click":
            self._on_change_cb = callback

    def click(self) -> None:
        """Simulate a button click."""
        if self._on_change_cb:
            self._on_change_cb(None)

    def props(self, spec: str | None = None, remove: str | None = None) -> None:
        """Stub for NiceGUI button.props()."""
        if remove is not None:
            self._props_disabled = False
        elif spec is not None and "disabled" in spec:
            self._props_disabled = True
        self._last_props = spec
        self._last_props_remove = remove


# ---------------------------------------------------------------------------
# _Proxy tests
# ---------------------------------------------------------------------------


class TestProxy:
    def test_get_reads_vm_property(self) -> None:
        vm = _StubVM("world")
        proxy = _Proxy(vm, "my_prop")
        assert proxy.value == "world"

    def test_set_writes_vm_property(self) -> None:
        vm = _StubVM("old")
        proxy = _Proxy(vm, "my_prop")
        proxy.value = "new"
        assert vm.my_prop == "new"

    def test_set_readonly_does_not_raise(self) -> None:
        vm = _ReadonlyVM("fixed")
        proxy = _Proxy(vm, "my_prop")
        proxy.value = "ignored"  # should not raise
        assert vm.my_prop == "fixed"


# ---------------------------------------------------------------------------
# bind() tests
# ---------------------------------------------------------------------------


class TestBind:
    def test_vm_to_ui_propagates_on_property_change(self) -> None:
        vm = _StubVM("initial")
        elem = _StubElement()

        sub = bind(vm, "my_prop", elem)
        try:
            vm._value = "updated"
            vm.emit_change("my_prop")
            assert elem.value == "updated"
        finally:
            sub.dispose()
            vm.dispose()

    def test_vm_to_ui_ignores_other_properties(self) -> None:
        vm = _StubVM("initial")
        elem = _StubElement("initial")

        sub = bind(vm, "my_prop", elem)
        try:
            vm.emit_change("other_prop")
            assert elem.value == "initial"  # unchanged
        finally:
            sub.dispose()
            vm.dispose()

    def test_ui_to_vm_proxy_bound(self) -> None:
        vm = _StubVM("start")
        elem = _StubElement()

        sub = bind(vm, "my_prop", elem)
        try:
            # Simulate NiceGUI writing to the proxy
            assert elem._bound_obj is not None
            elem._bound_obj.value = "from_ui"
            assert vm.my_prop == "from_ui"
        finally:
            sub.dispose()
            vm.dispose()

    def test_readonly_property_binds_without_error(self) -> None:
        vm = _ReadonlyVM("fixed")
        elem = _StubElement()

        sub = bind(vm, "my_prop", elem)
        try:
            vm.emit_change("my_prop")
            assert elem.value == "fixed"
        finally:
            sub.dispose()
            vm.dispose()

    def test_subscription_is_disposable(self) -> None:
        vm = _StubVM("v0")
        elem = _StubElement("v0")

        sub = bind(vm, "my_prop", elem)
        sub.dispose()

        # After dispose, changes should not propagate
        vm._value = "v1"
        vm.emit_change("my_prop")
        assert elem.value == "v0"  # not updated after dispose
        vm.dispose()


# ---------------------------------------------------------------------------
# bind_command() tests
# ---------------------------------------------------------------------------


class _StubCommand:
    """Minimal command stub."""

    def __init__(self, can: bool = True) -> None:
        self._can = can
        self._executed = False
        self._subject: Subject[None] = Subject()

    def can_execute(self, _: object = None) -> bool:
        return self._can

    def execute(self, _: object = None) -> None:
        self._executed = True

    @property
    def can_execute_changed(self) -> rx.Observable[None]:
        return self._subject

    def emit_can_execute_changed(self) -> None:
        self._subject.on_next(None)

    def dispose(self) -> None:
        self._subject.on_completed()
        self._subject.dispose()


class TestBindCommand:
    def test_click_invokes_execute(self) -> None:
        cmd = _StubCommand(can=True)
        btn = _StubElement()

        sub = bind_command(cmd, btn)
        try:
            btn.click()
            assert cmd._executed
        finally:
            sub.dispose()
            cmd.dispose()

    def test_initial_disabled_state_false(self) -> None:
        cmd = _StubCommand(can=True)
        btn = _StubElement()
        btn._props_disabled = False

        sub = bind_command(cmd, btn)
        try:
            assert not btn._props_disabled
        finally:
            sub.dispose()
            cmd.dispose()

    def test_initial_disabled_state_true(self) -> None:
        cmd = _StubCommand(can=False)
        btn = _StubElement()
        btn._props_disabled = False

        sub = bind_command(cmd, btn)
        try:
            assert btn._props_disabled
        finally:
            sub.dispose()
            cmd.dispose()

    def test_can_execute_changed_updates_disabled(self) -> None:
        cmd = _StubCommand(can=True)
        btn = _StubElement()
        btn._props_disabled = False

        sub = bind_command(cmd, btn)
        try:
            cmd._can = False
            cmd.emit_can_execute_changed()
            assert btn._props_disabled
        finally:
            sub.dispose()
            cmd.dispose()

    def test_re_enabled_removes_disabled(self) -> None:
        cmd = _StubCommand(can=False)
        btn = _StubElement()
        btn._props_disabled = False

        sub = bind_command(cmd, btn)
        try:
            assert btn._props_disabled

            cmd._can = True
            cmd.emit_can_execute_changed()
            assert not btn._props_disabled
        finally:
            sub.dispose()
            cmd.dispose()
