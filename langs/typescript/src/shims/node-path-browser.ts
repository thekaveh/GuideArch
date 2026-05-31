/**
 * Browser-compatible stub for Node.js `path` module.
 *
 * Provides the minimal surface used by scenario-loader.ts so the browser
 * bundle compiles. The schema-path discovery logic (findRepoRoot +
 * readFileSync) is reachable from Node tests; browser-mode Open in v1.0
 * goes through Toolbar._injectParsedScenario → vm._browserOpen using the
 * inlined schema, so the loader's filesystem fallback never fires in the
 * shipped UI (`spec/editors.md` §3).
 */

export function join(...parts: string[]): string {
  // Simple POSIX-style join (not platform-aware, but sufficient for the
  // Node-test code-path; browser-mode Open never reaches this stub).
  return parts.join('/').replace(/\/+/g, '/').replace(/\/$/, '');
}

export function dirname(p: string): string {
  return p.replace(/\/[^/]*$/, '') || '.';
}

export function resolve(...parts: string[]): string {
  return parts.join('/').replace(/\/+/g, '/');
}

export default { join, dirname, resolve };
