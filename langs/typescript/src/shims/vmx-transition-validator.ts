/**
 * Browser-compatible shim for the VMx lifecycle transition validator.
 *
 * The canonical VMx implementation (lifecycle/transitionValidator.ts) reads
 * the transition table from a JSON fixture file at runtime using readFileSync,
 * which is a Node.js-only API incompatible with browser/Tauri builds.
 *
 * This shim inlines the same fixture data so Vite can bundle it for the
 * browser target. The transition rules are identical to the fixture at:
 *   vendor/vmx/langs/typescript/src/fixtures/lifecycle-transitions.json
 *
 * Mapped via vite.config.js resolve.alias so Vite rewrites imports of
 * the transitionValidator from the VMx source tree to this shim instead.
 */

import { ConstructionStatus } from '../../../../vendor/vmx/langs/typescript/src/lifecycle/status.js';
import { StatusTransitionError } from '../../../../vendor/vmx/langs/typescript/src/lifecycle/exceptions.js';

// Inlined from lifecycle-transitions.json (VMx 2.1.0)
const _TRANSITIONS: Array<{
  from: string;
  via: string;
  to_intermediate: string | null;
  to_final: string | null;
  legal: boolean;
}> = [
  {
    from: 'Destructed',
    via: 'construct',
    to_intermediate: 'Constructing',
    to_final: 'Constructed',
    legal: true,
  },
  {
    from: 'Constructed',
    via: 'destruct',
    to_intermediate: 'Destructing',
    to_final: 'Destructed',
    legal: true,
  },
  {
    from: 'Constructed',
    via: 'reconstruct',
    to_intermediate: 'Destructing',
    to_final: 'Constructed',
    legal: true,
  },
  { from: 'Constructed', via: 'dispose', to_intermediate: null, to_final: 'Disposed', legal: true },
  { from: 'Destructed', via: 'dispose', to_intermediate: null, to_final: 'Disposed', legal: true },
  {
    from: 'Constructing',
    via: 'dispose',
    to_intermediate: null,
    to_final: 'Disposed',
    legal: true,
  },
  { from: 'Destructing', via: 'dispose', to_intermediate: null, to_final: 'Disposed', legal: true },
  { from: 'Disposed', via: 'construct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Disposed', via: 'destruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Disposed', via: 'reconstruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Disposed', via: 'dispose', to_intermediate: null, to_final: 'Disposed', legal: true },
  {
    from: 'Constructed',
    via: 'construct',
    to_intermediate: null,
    to_final: 'Constructed',
    legal: true,
  },
  {
    from: 'Destructed',
    via: 'destruct',
    to_intermediate: null,
    to_final: 'Destructed',
    legal: true,
  },
  { from: 'Constructing', via: 'construct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Destructing', via: 'destruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Destructed', via: 'reconstruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Constructing', via: 'destruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Constructing', via: 'reconstruct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Destructing', via: 'construct', to_intermediate: null, to_final: null, legal: false },
  { from: 'Destructing', via: 'reconstruct', to_intermediate: null, to_final: null, legal: false },
];

function _statusName(s: ConstructionStatus): string {
  return ConstructionStatus[s];
}

function _findRow(current: ConstructionStatus, operation: string) {
  const name = _statusName(current);
  return _TRANSITIONS.find((r) => r.from === name && r.via === operation);
}

export function isLegal(current: ConstructionStatus, operation: string): boolean {
  const row = _findRow(current, operation);
  return row !== undefined && row.legal;
}

export function requireTransition(current: ConstructionStatus, operation: string): void {
  if (!isLegal(current, operation)) {
    throw new StatusTransitionError(current, operation);
  }
}

export function finalState(current: ConstructionStatus, operation: string): ConstructionStatus {
  const row = _findRow(current, operation);
  if (row === undefined || !row.legal || row.to_final === null) {
    throw new StatusTransitionError(current, operation);
  }
  const key = row.to_final as keyof typeof ConstructionStatus;
  return ConstructionStatus[key];
}
