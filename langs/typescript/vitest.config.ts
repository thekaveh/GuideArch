/**
 * Vitest configuration — runs in Node.js, so Node built-ins (fs, path, url)
 * must NOT be redirected to the browser shims defined in vite.config.js.
 *
 * This file takes precedence over the `test` section inside vite.config.js
 * when Vitest is invoked directly. The top-level `resolve.alias` from
 * vite.config.js still applies here, but we explicitly override `fs`, `path`,
 * and `url` back to their real Node identities by omitting them from the alias
 * map (Vitest resolves un-aliased builtins to the actual Node modules).
 */
import { defineConfig } from 'vitest/config';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const vmxSrc = path.resolve(__dirname, '../../vendor/vmx/langs/typescript/src');

export default defineConfig({
  resolve: {
    alias: {
      // Replicate the VMx and rxjs aliases from vite.config.js — tests need them.
      vmx: path.join(vmxSrc, 'index.ts'),
      rxjs: path.resolve(__dirname, 'node_modules/rxjs'),
      'rxjs/operators': path.resolve(__dirname, 'node_modules/rxjs/operators'),
      // Intentionally DO NOT alias fs / path / url here — let Vitest resolve
      // them to the real Node.js built-in modules.
    },
  },
  test: {
    environment: 'node',
  },
});
