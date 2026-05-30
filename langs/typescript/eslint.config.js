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
    ignores: ['build/', 'dist/', '.svelte-kit/', 'node_modules/', 'src-tauri/target/'],
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
        EventTarget: 'readonly',
      },
    },
  },
];
