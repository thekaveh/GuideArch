# VMx 3.1 Audit Baseline

## GuideArch State

- Branch: `codex/vmx-3-1-refactor-audit`
- Pre-alignment GuideArch commit: `a80ee10ba511788db311f466f72134bd3ed6526c`
- Post-alignment working commit: `c0446ba032f8684c0b93eec5d274496a6dfb9b14`
- Intentionally ignored unrelated untracked files:
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-editors-tables.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-foundation.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-loose-ends-docs.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-overlays-results.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-shell-nav.md`
  - `docs/superpowers/specs/2026-06-22-ui-ux-elevation-design.md`

## VMx Source State

- Python constraint before: `vmx>=2.6.0`
- Python constraint after: `vmx==3.1.0`
- Python lockfile version before: `2.6.0`
- Python lockfile version after: `3.1.0`
- `vendor/vmx` commit before: `e2b23f879b262eb03db0e85b861a1324a2379d94`
- `vendor/vmx` commit after: `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12`
- TypeScript VMx version metadata: `vendor/vmx/langs/typescript/package.json` reports `3.1.0`
- C# VMx version metadata: `vendor/vmx/langs/csharp/src/VMx/VMx.csproj` reports `Version=3.1.0` and `MinSpecVersion=3.1.0`
- Python VMx version metadata: `vendor/vmx/langs/python/src/vmx/__about__.py` reports `__version__ = "3.1.0"` and `__min_spec_version__ = "3.1.0"`

## Verification

- `cd langs/python && uv sync --all-extras && uv run python - <<'PY' ... PY`
  - Result: `pass`
  - Key output: `3.1.0` and `ComponentVMOf MessageHub RxDispatcher`
- `cd vendor/vmx/langs/typescript && pnpm install && pnpm build`
  - Result: `fail`
  - Failure summary: `pnpm install` exited with `ERR_PNPM_IGNORED_BUILDS` for `esbuild@0.27.7`, so `pnpm build` did not run and no successful `dist` verification was produced.
- `cd langs/typescript && pnpm install && pnpm test`
  - Result: `fail`
  - Failure summary: `tests/unit/app-vm.test.ts` failed in `AppVM > publishes PropertyChangedMessage when theme changes`; expected observed property names to include `Model`, but the run saw `['model', 'modeledHint']`. Overall result: `1 failed | 307 passed`.
- `cd langs/csharp && dotnet test --nologo`
  - Result: `pass`
  - Key output: `GuideArch.Models.Tests` passed `30/30`; `GuideArch.ViewModels.Tests` passed `269/269`.
- `cd langs/python && uv run pytest tests/ -q`
  - Result: `fail`
  - Failure summary: test collection stopped with `ImportError: cannot import name 'RelayCommandOfT' from 'vmx.commands.relay_command'`; the error first surfaced from `langs/python/src/guidearch/viewmodels/app_vm.py` while collecting `tests/unit/test_app_vm.py`. Overall result: `7 errors during collection`.

## Compatibility Notes

- Python 3.1.0 smoke imports succeeded, but GuideArch's Python suite still imports `RelayCommandOfT`, which VMx 3.1.0 no longer exports from `vmx.commands.relay_command`. This blocks Python baseline test execution until a later compatibility/refactor task updates GuideArch's Python VMx usage.
- GuideArch TypeScript tests are largely compatible with the VMx 3.1.0 vendor source, but one assertion around `PropertyChangedMessage` property naming changed under the newer VMx behavior (`Model` vs observed `model` / `modeledHint`).
- Vendor VMx TypeScript package verification is currently blocked by pnpm's ignored-build policy for `esbuild@0.27.7`; this prevents Task 1 from proving a clean `pnpm build` inside `vendor/vmx/langs/typescript`.

## Post-Baseline Compatibility Fix

- Python command imports were updated from the removed `RelayCommandOfT` alias to the VMx 3.1 canonical `RelayCommandOf` in `langs/python/src/guidearch/viewmodels/app_vm.py` and `langs/python/src/guidearch/viewmodels/scenario_vm.py`.
- `langs/python/tests/unit/test_vmx31_command_compat.py` now guards that `AppVM.set_theme_cmd`, `ScenarioVM.open_cmd`, and `ScenarioVM.save_as_cmd` use `RelayCommandOf`.
- Fresh verification after the fix: `cd langs/python && uv run pytest tests/ -q` passed with `268 passed, 1 skipped, 3 warnings`; `cd langs/python && uv run ruff check src tests` passed.
- The TypeScript `AppVM > publishes PropertyChangedMessage when theme changes` test now asserts VMx 3.1's canonical lowercase `model` property notification instead of the old `Model` expectation.
- Fresh verification after the TypeScript fix: `cd langs/typescript && pnpm test` passed with `308 passed`; `cd langs/typescript && pnpm lint`, `pnpm format:check`, and `pnpm check` passed.
