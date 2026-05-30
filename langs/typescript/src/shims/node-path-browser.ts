/**
 * Browser-compatible stub for Node.js `path` module.
 *
 * Provides the minimal surface used by scenario-loader.ts so the browser
 * bundle compiles. The schema-path discovery logic (findRepoRoot + readFileSync)
 * is only exercised in Tauri/Node environments. In a pure browser context the
 * loader is unreachable until Tauri's File API provides a real filesystem path.
 */

export function join(...parts: string[]): string {
  // Simple POSIX-style join (not platform-aware, but sufficient for paths
  // that are only computed in Node/Tauri runtime, never in the browser).
  return parts.join('/').replace(/\/+/g, '/').replace(/\/$/, '');
}

export function dirname(p: string): string {
  return p.replace(/\/[^/]*$/, '') || '.';
}

export function resolve(..._parts: string[]): string {
  return _parts.join('/').replace(/\/+/g, '/');
}

export default { join, dirname, resolve };
