/**
 * Browser-compatible stub for Node.js `url` module (the `fileURLToPath` export).
 *
 * `fileURLToPath` is used only in scenario-loader.ts to locate the repo root
 * at module-load time. In a browser build the schema-discovery code path is
 * never executed (the loader throws before it reaches disk), so this stub is
 * sufficient to keep Rollup happy.
 */

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export function fileURLToPath(_url: string): string {
  throw new Error('fileURLToPath is not available in the browser');
}

export default { fileURLToPath };
