# ADR-0003: TOPSIS as in-repo code; no Microsoft Solver Foundation

**Status:** Accepted
**Date:** 2026-05-29

## Context

The legacy `.Old/GuideArch.Console` project references Microsoft Solver Foundation (MSF) but this reference is for a separate TSP demo. The actual GuideArch application uses a custom fuzzy TOPSIS algorithm implemented inline in `GuideArch.Model/Space.cs` — no external solver. MSF is also Windows/.NET-only and effectively unmaintained.

## Decision

Port the custom TOPSIS algorithm (ranking + critical-decisions + critical-constraints) directly into each implementation's `Models/Topsis/` namespace. No external constraint-solver dependency for v1. Document the algorithm formally in `spec/algorithms/topsis.md` during M1.

## Consequences

- No solver library is a runtime dependency.
- Each implementation owns a ~300-line TOPSIS port that conforms numerically to the spec.
- A pluggable solver backend (Z3 / MiniZinc / OR-Tools) is deferred to v1.3 for richer constraint kinds beyond threshold / dependency / conflict.
