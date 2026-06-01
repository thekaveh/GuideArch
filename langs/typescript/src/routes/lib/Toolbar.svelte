<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import type { ScenarioM } from '../../models/scenario.js';
  import type { AppVM } from '../../viewmodels/app-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import { SAMPLES } from '../../samples/index.js';

  export let vm: ScenarioVM;
  export let app: AppVM;
  export let onError: (msg: string) => void = () => {};

  $: themeStore = vmxToStore(app, 'theme');
  function toggleTheme() {
    app.setTheme($themeStore === 'dark' ? 'light' : 'dark');
  }

  let fileInputEl: HTMLInputElement;

  // Re-derive stores each time vm changes (for reactivity)
  $: filePathStore = vmxToStore(vm, 'filePath');
  $: scenarioStore = vmxToStore(vm, 'scenario');
  // v1.0 web-mode handleSave/handleSaveAs both anchor-download (Toolbar
  // bypasses vm.saveCmd, see spec/editors.md §3). Anchor-download needs
  // only a loaded scenario — the filename comes from filePath when set or
  // 'scenario.json' as fallback (Save) or a prompt() (Save As). The
  // earlier filePath-gated canSave matched saveCmd's predicate but not the
  // actual handler, so the Save button was incorrectly disabled after New
  // in browser mode even though the anchor-download would work fine.
  $: canSaveAs = $scenarioStore !== undefined;
  $: canSave = canSaveAs;

  // Single confirm-if-dirty gate shared by New, Open, and Open Sample —
  // each replaces the current scenario, so each should ask the same
  // question (the earlier code only gated New, which let an Open silently
  // discard edits).
  function _confirmDiscardIfDirty(action: string): boolean {
    return !vm.model.isDirty || confirm(`You have unsaved changes. ${action} anyway?`);
  }

  function handleNew() {
    if (!_confirmDiscardIfDirty('Create a new scenario')) return;
    vm.newCmd.execute();
  }

  function handleOpenClick() {
    if (!_confirmDiscardIfDirty('Open a scenario')) return;
    fileInputEl.click();
  }

  function handleOpenSample(index: number) {
    if (!_confirmDiscardIfDirty('Load a sample scenario')) return;
    const sample = SAMPLES[index];
    // _browserOpen is part of the ScenarioVM interface (declared in
    // scenario-vm.ts) and the factory always installs it, so the hook is
    // unconditionally present.
    vm._browserOpen(sample.raw, sample.id + '.json');
  }

  function handleFileChange(e: Event) {
    const input = e.currentTarget as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    // v1.0 unified UX: read via FileReader in both browser and Tauri runs
    // (the Tauri-1 `file.path` shortcut doesn't exist in Tauri 2 and falling
    // back to file.name fed openCmd a bare basename that always failed).
    // Tauri-native dialog support lands with @tauri-apps/plugin-dialog in
    // v1.1; see spec/editors.md §3.
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
    input.value = '';
  }

  /**
   * In browser mode, fs.readFileSync is shimmed to throw. We inject the parsed
   * scenario object directly into the VM model, bypassing the file loader.
   * The VM's _browserOpen hook is checked first; if absent (current impl),
   * we display an informational error.
   */
  function _injectParsedScenario(raw: unknown, fileName: string) {
    // _browserOpen is always defined on ScenarioVM — the previous
    // 'requires Tauri / Node.js fs' fallback was unreachable. Browser-mode
    // scenarios are loaded entirely client-side via the inlined schema.
    vm._browserOpen(raw, fileName);
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
    // v1.0 unified UX: anchor-download in both browser and Tauri runs.
    // Calling vm.saveCmd.execute() on the Tauri side hits fs.writeFileSync
    // which the Vite browser-shim throws on, so the previous Tauri branch
    // produced a spurious 'Save failed: …' toast. See spec/editors.md §3.
    const s = vm.model.scenario;
    if (!s) return;
    const blob = new Blob([_toPersistedJson(s)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = (vm.model.filePath ?? 'scenario.json') + '';
    a.click();
    URL.revokeObjectURL(url);
    vm._browserMarkSaved();
  }

  function handleSaveAs() {
    // v1.0 unified UX: prompt + anchor-download in both modes. The Tauri-
    // native Save dialog lands with @tauri-apps/plugin-dialog in v1.1
    // (spec/editors.md §3). Until then, an explicit Save-As that just
    // re-saved to the existing filePath was silently misleading; the
    // prompt-and-download path at least lets the user pick a new name.
    //
    // We deliberately do NOT call vm.saveAsCmd.execute() afterwards — the
    // VM's saveAs path goes through fs.writeFileSync which the Vite
    // browser-shim throws on, producing a spurious "Save failed: …" toast
    // after a successful download. The download itself is the save here;
    // the file_path/isDirty bookkeeping isn't meaningful.
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
    vm._browserMarkSaved(saveName);
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
    <button class="btn" disabled={!canSave} on:click={handleSave}>Save</button>
    <button class="btn" disabled={!canSaveAs} on:click={handleSaveAs}>Save As…</button>
  </div>
  <span class="spacer"></span>
  <button
    class="btn-icon"
    type="button"
    title={$themeStore === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
    aria-label={$themeStore === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
    on:click={toggleTheme}
  >
    {#if $themeStore === 'dark'}
      <!-- Sun: clicking will switch to light -->
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
        stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
      </svg>
    {:else}
      <!-- Moon: clicking will switch to dark -->
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
        stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79Z" />
      </svg>
    {/if}
  </button>
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

  /* Theme toggle: square icon button, ghost style */
  .btn-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    margin-right: 8px;
    background: transparent;
    color: var(--text-secondary);
    border: 1px solid var(--border-subtle);
    border-radius: 6px;
    cursor: pointer;
    transition: background-color 80ms ease-out, color 80ms ease-out, border-color 80ms ease-out;
  }

  .btn-icon:hover {
    background: var(--bg-surface-2);
    color: var(--text-primary);
    border-color: var(--border-strong);
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
