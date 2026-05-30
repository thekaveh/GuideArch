/**
 * Browser-compatible stub for Node.js `fs` module.
 *
 * The scenario-loader and scenario-vm use `fs.readFileSync` and
 * `fs.writeFileSync` at runtime — these are only reachable in Tauri/Node
 * environments, not pure browser. This stub exports the minimal surface so
 * the browser bundle compiles; calling these functions in a pure browser
 * context will throw at runtime (expected — the View must guard appropriately
 * by checking that the path comes from Tauri's File API before calling openCmd).
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
