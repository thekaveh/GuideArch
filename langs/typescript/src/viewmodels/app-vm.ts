/**
 * AppVM — root ViewModel and app-shell observables.
 *
 * Owns observable state that lives above any single scenario: the active
 * theme, the runtime mode, and the child ScenarioVM. Every View binds to
 * AppVM and navigates down to AppVM.scenario for scenario-specific state.
 *
 * Contract:
 *   - theme: string observable (canonical 'dark' + 'light' must work in
 *     all impls; impls may extend with framework-specific theme names).
 *   - mode: 'web' | 'native' | 'tauri', immutable after construction.
 *   - scenario: ScenarioVM, constant reference (only its internal state
 *     mutates; AppVM.scenario itself is never reassigned).
 *
 * Persistence: theme round-trips through localStorage under
 * "guidearch.theme". Missing / unreadable / malformed value silently
 * falls back to 'dark' and the next setTheme rewrites it.
 *
 * setTheme(name) validates against KNOWN_THEMES; an unknown name appends
 * a warning and leaves theme unchanged (never throws — the View's theme
 * picker should never blow up the app).
 */

import { ComponentVMOf, RelayCommandOf, MessageHub, NullDispatcher, type IMessageHub } from 'vmx';

import { makeScenarioVm, type ScenarioVM } from './scenario-vm.js';

// ---------------------------------------------------------------------------
// Theme set
// ---------------------------------------------------------------------------
// 'dark' and 'light' are MANDATED by spec across all impls. Additional
// framework-supported themes (e.g. high-contrast variants) can be added
// by calling registerTheme(name) at startup before the first AppVM constructs.
export const KNOWN_THEMES: Set<string> = new Set(['dark', 'light']);

export const DEFAULT_THEME = 'dark';

/**
 * Register an additional theme name (idempotent). Mirrors Python's
 * `register_theme(name)` and C#'s `AppVMFactory.RegisterTheme(name)`.
 */
export function registerTheme(name: string): void {
  KNOWN_THEMES.add(name);
}

// ---------------------------------------------------------------------------
// Persistence — small JSON-ish wrapper around localStorage
// ---------------------------------------------------------------------------
const STORAGE_KEY = 'guidearch.theme';

function loadPersistedTheme(): string {
  // Guard for SSR / Node-test envs where localStorage doesn't exist.
  if (typeof localStorage === 'undefined') return DEFAULT_THEME;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw && KNOWN_THEMES.has(raw)) return raw;
  } catch {
    // Quota exceeded, security policy, etc. — silently fall through.
  }
  return DEFAULT_THEME;
}

function persistTheme(theme: string): void {
  if (typeof localStorage === 'undefined') return;
  try {
    localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    // Storage failure is non-fatal — the live VM state wins, and the next
    // launch quietly falls back to default.
  }
}

// ---------------------------------------------------------------------------
// Mode detection
// ---------------------------------------------------------------------------
export type AppMode = 'web' | 'native' | 'tauri';

function detectMode(): AppMode {
  // Tauri runtime exposes the __TAURI__ global on the window object. Node
  // tests (vitest) don't have window. Anything else with a window is plain
  // browser/web mode.
  if (typeof window === 'undefined') return 'web';
  if (typeof (window as Window & { __TAURI__?: unknown }).__TAURI__ !== 'undefined') {
    return 'tauri';
  }
  return 'web';
}

// ---------------------------------------------------------------------------
// State shape
// ---------------------------------------------------------------------------
export interface AppState {
  theme: string;
  mode: AppMode;
  warnings: readonly string[];
}

// ---------------------------------------------------------------------------
// VM type
// ---------------------------------------------------------------------------
export type AppVM = ComponentVMOf<AppState> & {
  readonly setThemeCmd: RelayCommandOf<string>;
  readonly scenario: ScenarioVM;
  readonly hub: IMessageHub;

  /** Convenience — equivalent to setThemeCmd.execute(name). */
  setTheme(name: string): void;
};

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
export interface MakeAppVmOptions {
  /**
   * Inject a different theme set (testing or impl extension). When omitted,
   * the module-level KNOWN_THEMES is used.
   */
  knownThemes?: Set<string>;
  /**
   * Inject a custom load function (testing). When omitted, reads from
   * localStorage.
   */
  loadTheme?: () => string;
  /**
   * Inject a custom persist function (testing). When omitted, writes to
   * localStorage.
   */
  persistTheme?: (theme: string) => void;
  /**
   * Force a specific mode (testing). When omitted, detected from runtime.
   */
  mode?: AppMode;
  /**
   * Reuse an existing ScenarioVM (testing). When omitted, a fresh one is
   * constructed via makeScenarioVm().
   */
  scenario?: ScenarioVM;
  /**
   * Inject an explicit MessageHub (testing). When omitted, a fresh one is
   * constructed internally — and exposed as AppVM.hub either way.
   */
  hub?: MessageHub;
}

export function makeAppVm(opts: MakeAppVmOptions = {}): AppVM {
  const known = opts.knownThemes ?? KNOWN_THEMES;
  const load = opts.loadTheme ?? loadPersistedTheme;
  const save = opts.persistTheme ?? persistTheme;
  const mode = opts.mode ?? detectMode();
  const scenario = opts.scenario ?? makeScenarioVm();

  const hub = opts.hub ?? new MessageHub();

  let _vm: ComponentVMOf<AppState> | null = null;

  const initialTheme = load();
  // If load returned something the canonical KNOWN_THEMES doesn't recognise
  // (e.g. an impl extension wasn't registered this launch), fall back
  // silently — matches the "missing/malformed → default" contract.
  const startTheme = known.has(initialTheme) ? initialTheme : DEFAULT_THEME;

  function _getModel(): AppState {
    if (_vm === null) {
      return { theme: startTheme, mode, warnings: [] };
    }
    return _vm.model;
  }

  function _setState(patch: Partial<AppState>): void {
    if (_vm === null) return;
    _vm.model = { ..._vm.model, ...patch };
  }

  function setThemeImpl(name: string): void {
    if (!known.has(name)) {
      const msg = `Unknown theme: ${name}`;
      const warnings = [..._getModel().warnings, msg];
      _setState({ warnings });
      return;
    }
    if (name === _getModel().theme) return; // no-op, don't refire
    _setState({ theme: name });
    save(name);
  }

  const setThemeCmd = RelayCommandOf.builder<string>().task(setThemeImpl).build();

  const vm = ComponentVMOf.builder<AppState>()
    .name('app-vm')
    .model({ theme: startTheme, mode, warnings: [] })
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => `theme=${m.theme} mode=${m.mode}`)
    .build();

  vm.construct();
  _vm = vm;

  Object.defineProperties(vm, {
    setThemeCmd: {
      value: setThemeCmd,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    scenario: {
      value: scenario,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    hub: {
      value: hub,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    setTheme: {
      value: setThemeImpl,
      writable: false,
      enumerable: true,
      configurable: false,
    },
  });

  return vm as AppVM;
}
