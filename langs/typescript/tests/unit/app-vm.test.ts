/**
 * AppVM unit tests — the 5 mandatory cross-impl checks.
 *
 *   1. Default theme = 'dark' when persistence is empty.
 *   2. Theme round-trips through the persistence layer.
 *   3. Unknown theme is non-fatal (appends a warning, leaves state).
 *   4. property_changed fires when theme changes.
 *   5. mode is set at construction and immutable thereafter.
 */
import { describe, it, expect } from 'vitest';
import { MessageHub, PropertyChangedMessage } from 'vmx';

import {
  makeAppVm,
  DEFAULT_THEME,
  registerTheme,
  KNOWN_THEMES,
} from '../../src/viewmodels/app-vm.js';

function makeStubPersistence() {
  let stored: string | null = null;
  return {
    load: () => stored ?? DEFAULT_THEME,
    save: (v: string) => {
      stored = v;
    },
    peek: () => stored,
  };
}

describe('AppVM', () => {
  it('defaults to "dark" when nothing is persisted', () => {
    const stub = makeStubPersistence();
    const app = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
    });
    expect(app.model.theme).toBe(DEFAULT_THEME);
    expect(app.model.theme).toBe('dark');
  });

  it('round-trips theme through persistence', () => {
    const stub = makeStubPersistence();

    const app1 = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
    });
    app1.setTheme('light');
    expect(app1.model.theme).toBe('light');
    expect(stub.peek()).toBe('light');

    // A fresh AppVM reading the same persistence should restore 'light'.
    const app2 = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
    });
    expect(app2.model.theme).toBe('light');
  });

  it('appends a warning and leaves state when given an unknown theme', () => {
    const stub = makeStubPersistence();
    const app = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
    });

    const before = app.model.theme;
    expect(() => app.setTheme('chartreuse')).not.toThrow();
    expect(app.model.theme).toBe(before);
    expect(app.model.warnings.length).toBe(1);
    expect(app.model.warnings[0]).toContain('chartreuse');
    // Persistence layer must NOT have been touched.
    expect(stub.peek()).toBeNull();
  });

  it('publishes PropertyChangedMessage when theme changes', () => {
    const hub = new MessageHub();
    const stub = makeStubPersistence();
    const app = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
      hub,
    });

    const seen: string[] = [];
    hub.messages.subscribe((msg) => {
      if (msg instanceof PropertyChangedMessage) {
        seen.push(msg.propertyName);
      }
    });

    app.setTheme('light');

    // VMx 3.1 ComponentVMOf fires "model" when the whole model is reassigned.
    expect(seen).toContain('model');
  });

  it('exposes mode at construction; mode is immutable on the public surface', () => {
    const stub = makeStubPersistence();
    const app = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'tauri',
    });

    expect(app.model.mode).toBe('tauri');

    // There is no setMode command on the public AppVM surface.
    // (TypeScript-wise this is enforced by the AppVM type — runtime check
    // doubles as documentation that the contract was implemented.)
    expect((app as unknown as Record<string, unknown>).setMode).toBeUndefined();
  });

  it('exposes ScenarioVM as a stable .scenario reference', () => {
    const stub = makeStubPersistence();
    const app = makeAppVm({
      loadTheme: stub.load,
      persistTheme: stub.save,
      mode: 'web',
    });
    expect(app.scenario).toBeDefined();
    // setTheme must not replace the scenario reference.
    const before = app.scenario;
    app.setTheme('light');
    expect(app.scenario).toBe(before);
  });

  it('registerTheme makes a custom theme acceptable to setTheme', () => {
    // Mirrors Python register_theme / C# RegisterTheme parity surface.
    // registerTheme mutates the module-global KNOWN_THEMES set, so restore it
    // in finally to keep the test order-independent.
    registerTheme('high-contrast');
    try {
      const stub = makeStubPersistence();
      const app = makeAppVm({
        loadTheme: stub.load,
        persistTheme: stub.save,
        mode: 'web',
      });

      app.setTheme('high-contrast');
      expect(app.model.theme).toBe('high-contrast');
      expect(app.model.warnings.length).toBe(0);
      expect(stub.peek()).toBe('high-contrast');
    } finally {
      KNOWN_THEMES.delete('high-contrast');
    }
  });
});
