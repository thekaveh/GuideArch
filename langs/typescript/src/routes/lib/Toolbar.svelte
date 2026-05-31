<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import type { ScenarioM } from '../../models/scenario.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import { SAMPLES } from '../../samples/index.js';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  let fileInputEl: HTMLInputElement;

  // Re-derive stores each time vm changes (for reactivity)
  $: filePathStore = vmxToStore(vm, 'filePath');
  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: saveOff = $filePathStore === undefined || $scenarioStore === undefined;

  function handleNew() {
    if (vm.model.isDirty && !confirm('You have unsaved changes. Create a new scenario anyway?')) {
      return;
    }
    vm.newCmd.execute();
  }

  function handleOpenClick() {
    fileInputEl.click();
  }

  function handleOpenSample(index: number) {
    const sample = SAMPLES[index];
    const browserHook = (vm as ScenarioVM & { _browserOpen?: (r: unknown, n: string) => void })
      ._browserOpen;
    if (browserHook) {
      browserHook(sample.raw, sample.id + '.json');
    } else {
      onError('Sample loading requires _browserOpen hook on VM.');
    }
  }

  function handleFileChange(e: Event) {
    const input = e.currentTarget as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const isTauri = typeof (window as Window & { __TAURI__?: unknown }).__TAURI__ !== 'undefined';
    if (isTauri) {
      // In Tauri the File object has a non-standard .path property
      const fsPath = (file as typeof file & { path?: string }).path ?? file.name;
      vm.openCmd.execute(fsPath);
    } else {
      // Browser mode: read via FileReader, then parse and inject
      const reader = new FileReader();
      const fileName = file.name;
      reader.onload = (ev) => {
        const text = ev.target?.result as string;
        try {
          const raw = JSON.parse(text);
          _injectParsedScenario(raw, fileName);
        } catch (err) {
          const msg = err instanceof Error ? err.message : String(err);
          onError(`Failed to parse ${fileName}: ${msg}`);
        }
      };
      reader.readAsText(file);
    }
    input.value = '';
  }

  /**
   * In browser mode, fs.readFileSync is shimmed to throw. We inject the parsed
   * scenario object directly into the VM model, bypassing the file loader.
   * The VM's _browserOpen hook is checked first; if absent (current impl),
   * we display an informational error.
   */
  function _injectParsedScenario(raw: unknown, fileName: string) {
    const browserHook = (vm as ScenarioVM & { _browserOpen?: (r: unknown, n: string) => void })
      ._browserOpen;
    if (browserHook) {
      browserHook(raw, fileName);
    } else {
      onError(
        `Browser-mode file open: "${fileName}" was parsed but cannot be loaded without a Tauri runtime or Node.js fs. ` +
          'Run the app with Tauri to open local files.',
      );
    }
  }

  // The persisted JSON must not carry the runtime-only `warnings` field —
  // schema additionalProperties:false would reject it on re-open. The Tauri
  // save path in scenario-vm.ts strips it; mirror that here so browser-mode
  // Save/Save-As writes a re-openable file.
  function _toPersistedJson(scenario: ScenarioM): string {
    const { warnings: _w, ...persistable } = scenario as ScenarioM & { warnings?: unknown };
    return JSON.stringify(persistable, null, 2) + '\n';
  }

  function handleSave() {
    const isTauri = typeof (window as Window & { __TAURI__?: unknown }).__TAURI__ !== 'undefined';
    if (isTauri) {
      vm.saveCmd.execute();
    } else {
      // Browser: download as file
      const s = vm.model.scenario;
      if (!s) return;
      const blob = new Blob([_toPersistedJson(s)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = (vm.model.filePath ?? 'scenario.json') + '';
      a.click();
      URL.revokeObjectURL(url);
    }
  }

  function handleSaveAs() {
    const isTauri = typeof (window as Window & { __TAURI__?: unknown }).__TAURI__ !== 'undefined';
    if (isTauri) {
      // Tauri dialog would go here; for now fall through to browser behavior
      handleSave();
    } else {
      // Browser: prompt for file name, then download.
      // NB: we deliberately do NOT call vm.saveAsCmd.execute() afterwards —
      // the VM's saveAs path goes through fs.writeFileSync which the Vite
      // browser-shim throws on, producing a spurious "Save failed: …" toast
      // after a successful download. The download itself is the save in
      // browser mode; the file_path/isDirty bookkeeping isn't meaningful here.
      const suggested = vm.model.filePath ?? 'scenario.json';
      const saveName = prompt('Save as…', suggested);
      if (!saveName) return;
      const s = vm.model.scenario;
      if (!s) return;
      const blob = new Blob([_toPersistedJson(s)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = saveName;
      a.click();
      URL.revokeObjectURL(url);
    }
  }

  function handleSolve() {
    try {
      vm.solveCmd.execute();
    } catch (err) {
      if (err instanceof ScenarioMutationError) {
        onError(err.message);
      } else {
        throw err;
      }
    }
  }
</script>

<header class="toolbar">
  <span class="app-name">GuideArch</span>
  <div class="btn-group">
    <button class="btn" on:click={handleNew}>New</button>
    <button class="btn" on:click={handleOpenClick}>Open…</button>
    <input
      type="file"
      accept=".json"
      bind:this={fileInputEl}
      on:change={handleFileChange}
      style="display:none"
    />
    <button class="btn btn-sample" on:click={() => handleOpenSample(0)}>Open Sample SAS</button>
    <button class="btn btn-sample" on:click={() => handleOpenSample(1)}>Open Sample EDS</button>
    <button class="btn" disabled={saveOff} on:click={handleSave}>Save</button>
    <button class="btn" disabled={saveOff} on:click={handleSaveAs}>Save As…</button>
  </div>
  <span class="spacer"></span>
  <button class="btn btn-solve" disabled={$scenarioStore === undefined} on:click={handleSolve}>
    Solve
  </button>
</header>

<style>
  /* §6 Toolbar — 56px tall, 24px horizontal padding */
  .toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
    height: 56px;
    padding: 0 24px;
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
  }

  .app-name {
    font-weight: 600;
    font-size: 14px;
    color: var(--accent-hover);
    letter-spacing: 0.03em;
    margin-right: 8px;
  }

  .btn-group {
    display: flex;
    gap: 8px;
  }

  /* Ghost button — §5.1 */
  .btn {
    padding: 0 12px;
    height: 32px;
    background: transparent;
    color: var(--text-secondary);
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    transition:
      background 120ms ease-out,
      color 120ms ease-out;
    white-space: nowrap;
  }

  .btn:hover:not(:disabled) {
    background: var(--bg-surface-2);
    color: var(--text-primary);
  }

  .btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  /* Primary button — §5.1 — for sample openers */
  .btn-sample {
    background: var(--accent);
    color: var(--accent-on);
    border: none;
    font-weight: 600;
    padding: 0 16px;
  }

  .btn-sample:hover:not(:disabled) {
    background: var(--accent-hover);
    color: var(--accent-on);
  }

  .spacer {
    flex: 1;
  }

  /* Primary button — Solve */
  .btn-solve {
    background: var(--accent);
    color: var(--accent-on);
    border: none;
    font-weight: 600;
    padding: 0 16px;
  }

  .btn-solve:hover:not(:disabled) {
    background: var(--accent-hover);
    color: var(--accent-on);
  }
</style>
