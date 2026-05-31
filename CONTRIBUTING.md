# Contributing to GuideArch

Thanks for your interest in contributing. GuideArch is a spec-driven monorepo with three implementations kept in conformance by a shared corpus. The most important rule is: **the spec is the source of truth**, and **all three implementations must agree with it**.

## Before you start

- Read [docs/design/2026-05-29-guidearch-rewrite-design.md](docs/design/2026-05-29-guidearch-rewrite-design.md) for the architectural overview.
- Read [spec/ADRs/](spec/ADRs/) for the rationale behind specific decisions.
- Open an issue to discuss substantial changes before opening a PR.

## Workflow for a feature or fix

1. **Spec first.** If the change touches the domain or algorithm, update `spec/domain/` or `spec/algorithms/` first and, if relevant, add a new conformance scenario in `spec/conformance/scenarios/` with an expected output in `spec/conformance/expected/`.
2. **Implement in all three languages.** A feature is not "done" until TypeScript, C#, and Python all pass conformance for it.
3. **Use Conventional Commits.** Examples: `feat: add scenario comparison`, `fix(cs): off-by-one in topsis`.
4. **Open one PR.** Touching the spec and all three impls in one PR keeps reviewers oriented.

## Local development

- VMx is a git submodule at `vendor/vmx/`. Clone with `git clone --recurse-submodules`, or after cloning run `git submodule update --init`.
- Use `tools/use-vmx-local.sh` to consume VMx from the submodule (editable), or `tools/use-vmx-released.sh` to consume the published package versions.

## Tests

- Per-impl unit tests live under `langs/<impl>/tests/unit/`. Integration tests sit alongside in `langs/<impl>/tests/integration/`.
- Conformance:
  - TypeScript: `langs/typescript/src/conformance/` (CLI: `pnpm conformance`).
  - C#: `langs/csharp/src/GuideArch.Conformance/` (CLI: `dotnet run --project src/GuideArch.Conformance`).
  - Python: `langs/python/src/guidearch/conformance/` (CLI: `uv run python -m guidearch.conformance.runner`).
- CI runs both on every PR — per-impl workflows for unit, and `.github/workflows/conformance.yml` for cross-impl numerical conformance.

## Code style

Each command below comes in two flavors: **apply** (writes fixes) and **verify** (read-only, matches CI). Run *apply* locally before committing; CI runs *verify* and fails on any drift.

- TypeScript:
  - Apply: `pnpm lint && pnpm format`
  - Verify (CI): `pnpm lint && pnpm format:check`
- C#:
  - Apply: `dotnet format`
  - Verify (CI): `dotnet build && dotnet format --verify-no-changes`
  - Warnings are errors (set in `Directory.Build.props`).
- Python:
  - Apply: `uv run ruff check src tests --fix && uv run ruff format src tests`
  - Verify (CI): `uv run ruff check src tests && uv run ruff format --check src tests && uv run mypy src tests`

## Code of Conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
