# GuideArch — TypeScript

TypeScript + SvelteKit (Svelte 5 runes) + Tauri 2 implementation of GuideArch.

## Prerequisites

- Node 22+ (`node --version`)
- pnpm 11+ (`pnpm --version`; install via `npm install -g pnpm@latest`)
- Rust + cargo (only for `pnpm tauri dev` / `pnpm tauri build`; install via https://rustup.rs)
- The VMx submodule initialised at `vendor/vmx/` (run `git submodule update --init` from the repo root if not)

## Dev setup

```bash
cd langs/typescript
pnpm install
```

## Run

```bash
pnpm dev          # web mode — browser at http://localhost:1420
pnpm tauri dev    # desktop mode — native Tauri window (first build is slow; subsequent are fast)
```

After the app loads, click **Open Sample SAS** or **Open Sample EDS** in the toolbar to try a bundled scenario.

v1.0 uses the same browser-mode UX in both browser and Tauri runs: `<input type="file">` for Open, anchor-download (`URL.createObjectURL`) for Save / Save-As. The OS-native picker integration via `@tauri-apps/plugin-dialog` is on the v1.1 backlog (`spec/editors.md` §3).

## Build (production)

```bash
pnpm build        # SvelteKit static site → langs/typescript/build/
pnpm tauri build  # produces signed-or-unsigned installers per OS
```

## Test

```bash
pnpm test            # vitest — unit + integration + conformance
pnpm conformance     # standalone numerical-conformance runner
pnpm lint            # eslint
pnpm format:check    # prettier (check-only; CI gate)
pnpm format          # prettier (write)
```

The integration suite at `tests/integration/vm-mvvm.test.ts` exercises the `ScenarioVM` and its children **without mounting any Svelte components** — that's the MVVM separation in action.

## Architecture notes

- **VMx browser-shim**: VMx-TS's published `dist/` calls `node:fs` / `node:url` at module-init. We work around this with a Vite plugin (`vmx-browser-shim` in `vite.config.js`) plus a `resolve.alias` from `vmx` to the VMx source TypeScript. Same shim handles `scenario-loader.ts`'s use of `fs`. See ADR-0001 §"VMx improvements flow back upstream" — the long-term fix lives in the `vendor/vmx` submodule, not in this repo.
- **Browser-mode openCmd**: `Toolbar.svelte` reads the picked file via `FileReader.readAsText`, calls `JSON.parse`, then hands the raw object to `vm._browserOpen(raw, filename)` which validates via an inlined copy of `scenario.schema.json` at `src/samples/scenario.schema.json`. This avoids any filesystem access in the browser.
- **Sample scenarios** ship as JSON imports under `src/samples/` so they tree-shake into the bundle.
