# ADR-0001: Three implementations sharing one spec; VMx as submodule

**Status:** Accepted
**Date:** 2026-05-29

## Context

GuideArch was a Silverlight application. Silverlight is end-of-life. We want a modernized rewrite that simultaneously serves as a real-world demonstration of the VMx MVVM framework across all the languages VMx supports.

## Decision

Ship three concurrent implementations (TypeScript, C#, Python) of the same application, sharing one language-neutral spec under `spec/`. Vendor VMx as a git submodule at `vendor/vmx/` (pinned to a stable tag), consumed via local/editable installs during development and via published packages (npm / NuGet / PyPI) for downstream users.

## Consequences

- All three implementations must stay in conformance with the spec, enforced by CI.
- New features land in all three before any tag — this is a real scope multiplier (~3×) but the strongest cross-language proof of VMx.
- VMx improvements discovered during GuideArch work flow back upstream via PR against the VMx submodule.
- Single monorepo version (see ADR-0005) means release cadence is gated on the slowest impl.
