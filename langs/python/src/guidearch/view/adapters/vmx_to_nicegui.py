"""VMx → NiceGUI binding adapter.

Binding semantics
-----------------
``bind(vm, prop_name, ui_element)``
    Two-way binding between a VMx observable property and a NiceGUI element.

    - VM → UI: subscribes to ``vm.property_changed`` (an ``rx.Observable[str]``
      emitting property name strings) and calls ``ui_element.set_value(getattr(vm,
      prop_name))`` on each matching emission.
    - UI → VM: calls ``ui_element.bind_value(obj, attr)`` where a thin adapter
      object proxies writes back to ``setattr(vm, prop_name, ...)``.  If the
      property has no setter (read-only), only the VM→UI direction is wired.

``bind_command(cmd, button)``
    Wires a RelayCommand (or RelayCommandOfT) to a NiceGUI button.

    - Click: calls ``cmd.execute()``.
    - Enabled/disabled: subscribes to ``cmd.can_execute_changed`` and updates
      ``button.props('disabled=...')``.  Initial state is applied immediately.

Size: ~70 LOC of functional code (excluding this docstring and type annotations).

Notes on NiceGUI's bind_value
------------------------------
NiceGUI's ``element.bind_value(source_obj, attr_name)`` watches the attribute on
``source_obj`` and keeps the element's ``value`` in sync.  To proxy VM property
writes back from the element we introduce a ``_Proxy`` shim that translates
``setattr(proxy, 'value', v)`` into ``setattr(vm, prop_name, v)`` and
``getattr(proxy, 'value')`` into ``getattr(vm, prop_name)``.
"""

from __future__ import annotations

import contextlib
from typing import Any, cast

import reactivex as rx
from reactivex.abc import DisposableBase


class _Proxy:
    """Thin attribute shim that bridges NiceGUI's bind_value to a VMx property."""

    def __init__(self, vm: Any, prop_name: str) -> None:
        object.__setattr__(self, "_vm", vm)
        object.__setattr__(self, "_prop_name", prop_name)

    @property
    def value(self) -> Any:
        vm = object.__getattribute__(self, "_vm")
        prop = object.__getattribute__(self, "_prop_name")
        return getattr(vm, prop)

    @value.setter
    def value(self, v: Any) -> None:
        vm = object.__getattribute__(self, "_vm")
        prop = object.__getattribute__(self, "_prop_name")
        with contextlib.suppress(AttributeError):
            setattr(vm, prop, v)


def bind(
    vm: Any,
    prop_name: str,
    ui_element: Any,
) -> DisposableBase:
    """Set up two-way binding between *vm.prop_name* and *ui_element*.

    Returns the RxPY subscription so the caller can dispose it on teardown.
    The NiceGUI bind_value link lives for the element's lifetime and is not
    explicitly disposable (NiceGUI manages it internally).

    Parameters
    ----------
    vm:
        A VMx ViewModel that exposes a ``property_changed`` observable and
        the named property as a Python attribute.
    prop_name:
        The VM property name to bind.
    ui_element:
        A NiceGUI element with ``.set_value()``, ``.bind_value()``, and ``.value``.
    """
    proxy = _Proxy(vm, prop_name)

    # UI → VM: NiceGUI keeps ui_element.value in sync with proxy.value;
    # proxy.value setter writes back to vm.prop_name.
    with contextlib.suppress(Exception):
        ui_element.bind_value(proxy, "value")

    # VM → UI: push changes when the VM emits a matching property change.
    def _on_property_changed(changed_name: str) -> None:
        if changed_name == prop_name:
            with contextlib.suppress(Exception):
                ui_element.set_value(getattr(vm, prop_name))

    # Subscribe to property_changed observable
    prop_changed: rx.Observable[str] = vm.property_changed
    return prop_changed.subscribe(on_next=_on_property_changed)


def bind_command(
    cmd: Any,
    button: Any,
) -> DisposableBase:
    """Wire *cmd* to *button*.

    - Click invokes ``cmd.execute()``.
    - ``can_execute`` state is reflected as ``disabled`` on the button.

    Parameters
    ----------
    cmd:
        A VMx RelayCommand (or RelayCommandOfT) with ``execute()``,
        ``can_execute()``, and ``can_execute_changed`` observable.
    button:
        A NiceGUI ``ui.button`` instance.
    """
    # Wire click
    button.on("click", lambda _: cmd.execute())

    # Apply initial disabled state
    _set_disabled(button, not cmd.can_execute())

    # Subscribe to future can_execute changes
    def _on_can_execute_changed(_: Any) -> None:
        _set_disabled(button, not cmd.can_execute())

    return cast(
        DisposableBase,
        cmd.can_execute_changed.subscribe(on_next=_on_can_execute_changed),
    )


def _set_disabled(button: Any, disabled: bool) -> None:
    """Apply disabled prop to a NiceGUI button, ignoring errors gracefully."""
    with contextlib.suppress(Exception):
        if disabled:
            button.props("disabled")
        else:
            button.props(remove="disabled")
