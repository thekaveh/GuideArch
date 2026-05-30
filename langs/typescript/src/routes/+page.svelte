<script lang="ts">
  import { helloVMx } from '$lib/hello-vmx';

  let greeting = $state(helloVMx());
</script>

<main>
  <h1>GuideArch</h1>
  <p class="subtitle">TypeScript + Tauri + Svelte 5 implementation — bootstrap</p>
  <p class="vmx-proof">{greeting}</p>
</main>

<style>
  main { font-family: system-ui, sans-serif; padding: 3rem; max-width: 720px; margin: 0 auto; }
  h1 { font-size: 2.5rem; margin: 0 0 .5rem; }
  .subtitle { color: #666; margin: 0 0 2rem; }
  .vmx-proof { padding: 1rem; background: #f4f4f4; border-radius: 4px; font-family: monospace; }
</style>
