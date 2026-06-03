<script lang="ts">
  import { confirmRequest } from './confirm-dialog.js';

  function decide(ok: boolean) {
    const req = $confirmRequest;
    if (req !== null) {
      req.resolve(ok);
      confirmRequest.set(null);
    }
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') decide(false);
    if (e.key === 'Enter') decide(true);
  }
</script>

{#if $confirmRequest !== null}
  <div class="confirm-overlay" on:click={() => decide(false)} role="presentation">
    <div
      class="confirm"
      role="alertdialog"
      aria-modal="true"
      aria-label={$confirmRequest.title}
      tabindex="-1"
      on:click|stopPropagation
      on:keydown={onKeydown}
    >
      <div class="title-row">
        {#if $confirmRequest.destructive}
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            class="icon danger"
            aria-hidden="true"
          >
            <path
              d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
            />
            <line x1="12" x2="12" y1="9" y2="13" />
            <line x1="12" x2="12.01" y1="17" y2="17" />
          </svg>
        {:else}
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            class="icon accent"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="10" />
            <line x1="12" x2="12" y1="16" y2="12" />
            <line x1="12" x2="12.01" y1="8" y2="8" />
          </svg>
        {/if}
        <h2 class="title">{$confirmRequest.title}</h2>
      </div>
      <p class="body">{$confirmRequest.body}</p>
      <div class="actions">
        <button class="btn-cancel" on:click={() => decide(false)}>
          {$confirmRequest.cancelLabel ?? 'Cancel'}
        </button>
        <!-- autofocus is the WCAG-recommended pattern for alertdialog: focus
             moves to the primary action on open so keyboard users don't have
             to Tab from the invoker. -->
        <!-- svelte-ignore a11y_autofocus -->
        <button
          class="btn-confirm"
          class:destructive={$confirmRequest.destructive}
          autofocus
          on:click={() => decide(true)}
        >
          {$confirmRequest.confirmLabel ?? 'Confirm'}
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  .confirm-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.65);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 100;
    animation: overlay-in 80ms ease-out;
  }

  @keyframes overlay-in {
    from {
      opacity: 0;
    }
    to {
      opacity: 1;
    }
  }

  .confirm {
    background: var(--bg-surface-3);
    border: 1px solid var(--border-strong);
    border-radius: 8px;
    padding: 20px 24px 20px;
    max-width: 28rem;
    width: 90vw;
    box-shadow: 0 12px 40px rgba(0, 0, 0, 0.6);
    animation: dialog-in 120ms ease-out;
  }

  @keyframes dialog-in {
    from {
      opacity: 0;
      transform: translateY(8px) scale(0.98);
    }
    to {
      opacity: 1;
      transform: none;
    }
  }

  .title-row {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-bottom: 8px;
  }

  .icon.danger {
    color: var(--danger);
    flex-shrink: 0;
  }

  .icon.accent {
    color: var(--accent-hover);
    flex-shrink: 0;
  }

  .title {
    margin: 0;
    font-size: 15px;
    color: var(--text-primary);
    font-weight: 600;
  }

  .body {
    margin: 0 0 20px;
    font-size: 13px;
    color: var(--text-secondary);
    line-height: 1.55;
    word-break: break-word;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }

  .btn-cancel {
    padding: 7px 14px;
    background: transparent;
    color: var(--text-secondary);
    border: 1px solid var(--border-strong);
    border-radius: 6px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    transition:
      background-color 80ms ease-out,
      color 80ms ease-out;
  }

  .btn-cancel:hover {
    background: var(--bg-surface-2);
    color: var(--text-primary);
  }

  .btn-confirm {
    padding: 7px 16px;
    background: var(--accent);
    color: var(--accent-on);
    border: 1px solid var(--accent);
    border-radius: 6px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 600;
    transition:
      background-color 80ms ease-out,
      border-color 80ms ease-out;
  }

  .btn-confirm:hover {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
  }

  .btn-confirm.destructive {
    background: var(--danger);
    border-color: var(--danger);
    color: #ffffff;
  }

  .btn-confirm.destructive:hover {
    background: color-mix(in srgb, var(--danger) 85%, white 15%);
    border-color: color-mix(in srgb, var(--danger) 85%, white 15%);
  }
</style>
