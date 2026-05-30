"""Smoke-test VMx wiring by instantiating a trivial ComponentVMOf."""

from __future__ import annotations

from dataclasses import dataclass

from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB, ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.message_hub import MessageHub


@dataclass(frozen=True)
class Greeting:
    message: str


def hello_vmx() -> str:
    # NULL_MESSAGE_HUB is typed `MessageHub[Any]` in VMx; we want it as
    # MessageHub[Message] here. Null-object pattern — the parameter type
    # doesn't matter at runtime since the hub never dispatches.
    hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
    vm: ComponentVMOf[Greeting] = (
        ComponentVMOf[Greeting].builder()
        .name("greeting-vm")
        .hint("GuideArch greeting")
        .services(hub, NULL_DISPATCHER)
        .model(Greeting("hello from VMx"))
        .modeled_hinter(lambda g: g.message)
        .build()
    )
    vm.construct()
    return (
        f'VMx loaded — model.message = "{vm.model.message}", '
        f"status = {vm.status.name}"
    )
