import { defineConfig } from "vite";
import { sveltekit } from "@sveltejs/kit/vite";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Absolute path to the vendored VMx TypeScript package source
const vmxSrc = path.resolve(__dirname, "../../vendor/vmx/langs/typescript/src");

// The transitionValidator source file (Node-dependent, needs shimming for browser)
const transitionValidatorSrc = path.resolve(
  vmxSrc,
  "lifecycle/transitionValidator.ts"
);

// Our browser-compatible shim
const transitionValidatorShim = path.resolve(
  __dirname,
  "src/shims/vmx-transition-validator.ts"
);

/** Vite plugin: redirect imports of transitionValidator to our browser shim. */
function vmxBrowserShimPlugin() {
  return {
    name: "vmx-browser-shim",
    enforce: "pre",
    resolveId(id, importer) {
      // Match both the .js (ESM import style) and .ts (actual file) paths
      if (
        importer &&
        (importer.includes("vendor/vmx") || importer.includes("node_modules/vmx"))
      ) {
        if (
          id.includes("transitionValidator") ||
          id === "./transitionValidator.js" ||
          id === "../lifecycle/transitionValidator.js"
        ) {
          return transitionValidatorShim;
        }
      }
      // Also catch if the resolved file itself is the transitionValidator
      return null;
    },
    load(id) {
      if (id === transitionValidatorSrc) {
        // Redirect loads of the actual transitionValidator.ts to our shim
        return null; // Let resolveId handle it
      }
      return null;
    },
  };
}

// @ts-expect-error process is a nodejs global
const host = process.env.TAURI_DEV_HOST;

// https://vite.dev/config/
export default defineConfig(async () => ({
  plugins: [vmxBrowserShimPlugin(), sveltekit()],

  resolve: {
    alias: {
      // Resolve `vmx` imports to the VMx TypeScript source entry point
      // (bypasses the pre-built dist, which uses Node.js readFileSync).
      "vmx": path.join(vmxSrc, "index.ts"),
    },
  },

  // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
  //
  // 1. prevent Vite from obscuring rust errors
  clearScreen: false,
  // 2. tauri expects a fixed port, fail if that port is not available
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      // 3. tell Vite to ignore watching `src-tauri`
      ignored: ["**/src-tauri/**"],
    },
  },
}));
