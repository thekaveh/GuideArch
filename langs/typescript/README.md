# GuideArch — TypeScript

TypeScript + SvelteKit (Svelte 5 runes) + Tauri 2 implementation of GuideArch.

The UI renders the shared two-theme design system (`spec/design-system.md`) via Svelte CSS custom properties — an elevated dark default and a fully-retinted light theme, toggled from the toolbar.

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

After the app loads, click **Sample SAS** or **Sample EDS** in the toolbar (or the **Open Sample SAS** CTA on the first-launch hero) to try a bundled scenario.

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

- **Node builtin aliases**: browser builds alias `node:fs`, `node:path`, and
  `node:url` to browser-safe stubs in `vite.config.js`. The previous
  `vmx-browser-shim` plugin was removed after VMx 3.1 made the transition
  validator bundle cleanly from source. The remaining aliases support local
  source modules such as `scenario-loader.ts`; browser-mode open uses the raw
  object path below instead of filesystem access.
- **Browser-mode openCmd**: `Toolbar.svelte` reads the picked file via `FileReader.readAsText`, calls `JSON.parse`, then hands the raw object to `vm._browserOpen(raw, filename)` which validates via an inlined copy of `scenario.schema.json` at `src/samples/scenario.schema.json`. This avoids any filesystem access in the browser.
- **Sample scenarios** ship as JSON imports under `src/samples/` so they tree-shake into the bundle.
