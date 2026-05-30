<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';

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

  function handleSave() {
    const isTauri = typeof (window as Window & { __TAURI__?: unknown }).__TAURI__ !== 'undefined';
    if (isTauri) {
      vm.saveCmd.execute();
    } else {
      // Browser: download as file
      const s = vm.model.scenario;
      if (!s) return;
      const blob = new Blob([JSON.stringify(s, null, 2) + '\n'], { type: 'application/json' });
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
      // Browser: prompt for file name, then download
      const suggested = vm.model.filePath ?? 'scenario.json';
      const saveName = prompt('Save as…', suggested);
      if (!saveName) return;
      const s = vm.model.scenario;
      if (!s) return;
      const blob = new Blob([JSON.stringify(s, null, 2) + '\n'], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = saveName;
      a.click();
      URL.revokeObjectURL(url);
      vm.saveAsCmd.execute(saveName);
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
    <button class="btn" disabled={saveOff} on:click={handleSave}>Save</button>
    <button class="btn" disabled={saveOff} on:click={handleSaveAs}>Save As…</button>
  </div>
  <span class="spacer"></span>
  <button class="btn btn-solve" disabled={$scenarioStore === undefined} on:click={handleSolve}>
    Solve
  </button>
</header>

<style>
  .toolbar {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.6rem 1.25rem;
    background: #1a1a20;
    border-bottom: 1px solid #2e2e38;
    flex-shrink: 0;
  }

  .app-name {
    font-weight: 700;
    font-size: 1.05rem;
    color: #a78bfa;
    letter-spacing: 0.03em;
    margin-right: 0.75rem;
  }

  .btn-group {
    display: flex;
    gap: 0.4rem;
  }

  .btn {
    padding: 0.3rem 0.85rem;
    background: #2d2d3a;
    color: #e8e8ec;
    border: 1px solid #3e3e50;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.82rem;
    transition: background 0.12s;
  }

  .btn:hover:not(:disabled) {
    background: #3d3d50;
  }

  .btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }

  .spacer {
    flex: 1;
  }

  .btn-solve {
    background: #4f46e5;
    border-color: #4f46e5;
    font-weight: 600;
  }

  .btn-solve:hover:not(:disabled) {
    background: #6366f1;
    border-color: #6366f1;
  }
</style>
