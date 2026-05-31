/**
 * Browser-compatible stub for Node.js `fs` module.
 *
 * scenario-loader and scenario-vm reference `fs.readFileSync` and
 * `fs.writeFileSync` so that a Node-context test or future Tauri-native
 * integration can read/write directly. v1.0 ships a single browser-mode
 * UX in both browser and Tauri runs (`spec/editors.md` §3): Toolbar.svelte
 * loads files via FileReader + `_browserOpen` and writes via anchor-download,
 * so neither stubbed call is reached at runtime. The stubs make the
 * browser bundle compile and throw loudly if a future caller routes
 * through the VM's fs path before the v1.1 plugin-dialog integration.
 */

/* eslint-disable @typescript-eslint/no-unused-vars */
export function readFileSync(_path: string, _encoding?: string): string {
  throw new Error('fs.readFileSync is not available in the browser');
}

export function writeFileSync(_path: string, _data: string, _encoding?: string): void {
  throw new Error('fs.writeFileSync is not available in the browser');
}

export function existsSync(_path: string): boolean {
  return false;
}
/* eslint-enable @typescript-eslint/no-unused-vars */

export default {
  readFileSync,
  writeFileSync,
  existsSync,
};
