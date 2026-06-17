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

## Update — 2026-06-16: Python consumes VMx from PyPI

VMx is now published to PyPI as [`vmx`](https://pypi.org/project/vmx/). The **Python** impl therefore consumes VMx as an ordinary PyPI dependency (`vmx>=2.6.0` in `langs/python/pyproject.toml`) rather than building it from the submodule — the conventional, downstream-faithful arrangement, and it lets the Python build, Docker image, and `python.yml` CI run without checking out `vendor/vmx/`.

The submodule is **retained** because the **TypeScript** (npm) and **C#** (NuGet) packages are not yet published; those two still build VMx from `vendor/vmx/`. The local/editable workflow above is preserved for all three via `tools/use-vmx-local.sh` (and `use-vmx-released.sh` reverts Python to PyPI), so VMx fixes can still be co-developed and flowed upstream. When VMx ships to npm and NuGet, TS and C# can migrate the same way and the submodule can be retired.

### Accepted consequence: a temporary VMx version skew

PyPI currently publishes only VMx `2.6.0`, while the pinned `vendor/vmx/` submodule builds VMx `2.1.x`. So Python runs VMx `2.6.0` and TS/C# run `2.1.x` until the submodule pin is advanced (or npm/NuGet publish `2.6.0`). This skew is **accepted as temporary**: the GuideArch conformance corpus is numerical (M1) and the three impls produce byte-identical expected outputs under both VMx minors — i.e. no observable GuideArch behavior depends on the 2.1→2.6 delta, and CI stays green across the gap. Closing the skew (bumping the submodule pin to the `2.6.0` commit, which would also re-validate TS/C# against the newer VMx) is deferred follow-up work, tracked separately from this Python-only migration. This does not violate the ADR-0005 lockstep-version rule, which governs the GuideArch *application* version (`1.0.0` in all three), not the VMx framework dependency.
