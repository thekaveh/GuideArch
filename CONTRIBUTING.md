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

- Per-impl unit tests live under `langs/<impl>/tests/unit/`.
- Conformance tests live under `langs/<impl>/tests/conformance/` and consume `spec/conformance/`.
- CI runs both on every PR.

## Code style

- TypeScript: ESLint + Prettier — `pnpm lint && pnpm format`.
- C#: Treat warnings as errors. `dotnet format`.
- Python: ruff + mypy --strict — `uv run ruff check src && uv run mypy src`.

## Code of Conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
