# ADR-0005: Single monorepo version; all three impls release together

**Status:** Accepted
**Date:** 2026-05-29

## Context

A tri-impl monorepo can either ship per-language versions (`ts-v1.0.0`, `cs-v1.0.0`, …) or a single unified version (`v1.0.0` releases all three).

## Decision

Single monorepo version. One `v<major>.<minor>.<patch>` tag publishes all three implementations. No release until all three pass conformance and a manual smoke test.

## Consequences

- Easier to reason about parity: "what features are in v1.2?" has one answer.
- One CHANGELOG.
- Release cadence is gated on the slowest impl. If C# is a sprint behind, the whole release waits.
- If the gating becomes a recurring problem post-v1, switch to per-lang versions in an ADR amendment.
