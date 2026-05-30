# ADR-0004: MIT License

**Status:** Accepted
**Date:** 2026-05-29

## Context

GuideArch is a small, academically-rooted tool. VMx (its primary dependency) is MIT-licensed. We evaluated MIT vs. Apache 2.0.

## Decision

Adopt the MIT License. Match VMx; keep license text minimal; preserve the option to relicense to Apache 2.0 later if contributor base or patent exposure change.

## Consequences

- One-off contributors face a ~170-word license rather than ~10,000 words.
- No explicit patent grant or retaliation clause. We accept this risk for a non-patent-heavy domain.
- Apache-2.0 dependencies (Tauri, Avalonia, NiceGUI) can be freely consumed under MIT — license compatibility is one-way fine.
- Future relicense to Apache 2.0 requires sole-copyright or sign-off from all contributors.
