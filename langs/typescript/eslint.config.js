import js from '@eslint/js';
import ts from 'typescript-eslint';
import svelte from 'eslint-plugin-svelte';
import svelteParser from 'svelte-eslint-parser';
import tsParser from '@typescript-eslint/parser';

export default [
  js.configs.recommended,
  ...ts.configs.recommended,
  ...svelte.configs['flat/recommended'],
  {
    ignores: [
      'build/',
      'dist/',
      '.svelte-kit/',
      'node_modules/',
      'src-tauri/target/',
      // Visual snapshot scripts are manually-run Node.js ESM scripts, not TypeScript source.
      'tests/visual/',
    ],
  },
  {
    files: ['**/*.ts'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        ecmaVersion: 2022,
        sourceType: 'module',
      },
    },
  },
  {
    files: ['**/*.svelte'],
    languageOptions: {
      parser: svelteParser,
      parserOptions: {
        parser: tsParser,
      },
      // Svelte components run in the browser — expose standard browser globals
      // so the linter does not flag built-in DOM types as undefined.
      globals: {
        FileList: 'readonly',
        File: 'readonly',
        Event: 'readonly',
        HTMLInputElement: 'readonly',
        HTMLSelectElement: 'readonly',
        EventTarget: 'readonly',
        Window: 'readonly',
        FocusEvent: 'readonly',
        KeyboardEvent: 'readonly',
        CustomEvent: 'readonly',
        URL: 'readonly',
        Blob: 'readonly',
        FileReader: 'readonly',
        confirm: 'readonly',
        prompt: 'readonly',
        document: 'readonly',
        window: 'readonly',
      },
    },
    rules: {
      // @typescript-eslint/no-unused-vars crashes on certain Svelte 5 reactive
      // patterns due to a known incompatibility between the TS-ESLint plugin and
      // the Svelte-ESLint parser (github.com/sveltejs/eslint-plugin-svelte/issues/652).
      // Disable both the TS and JS variants for .svelte files; the Svelte plugin
      // itself enforces unused-import hygiene via svelte/no-unused-svelte-ignore.
      '@typescript-eslint/no-unused-vars': 'off',
      'no-unused-vars': 'off',
    },
  },
];
