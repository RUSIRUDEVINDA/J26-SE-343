/**
 * Client-side guards for officer valuation saves.
 * A stable idempotency key is reused for retries of the same attempt and cleared only after success.
 */

export type ValuationSaveAttemptState = {
  /** Key reused for retries of the current save attempt; null when idle. */
  idempotencyKey: string | null;
  /** True while a save request is in flight. */
  inFlight: boolean;
};

export type BeginValuationSaveResult =
  | { ok: false; reason: "in_flight" }
  | { ok: true; idempotencyKey: string; nextState: ValuationSaveAttemptState };

/** Blocks overlapping submits (double-click / rapid re-entry). */
export function shouldBlockValuationSave(inFlight: boolean): boolean {
  return inFlight;
}

/**
 * Starts or continues a save attempt. Reuses the existing key on retry after failure;
 * allocates a new key only when starting a fresh attempt.
 */
export function beginValuationSaveAttempt(
  state: ValuationSaveAttemptState,
  createKey: () => string = () => crypto.randomUUID(),
): BeginValuationSaveResult {
  if (shouldBlockValuationSave(state.inFlight)) {
    return { ok: false, reason: "in_flight" };
  }

  const idempotencyKey = state.idempotencyKey ?? createKey();
  return {
    ok: true,
    idempotencyKey,
    nextState: { idempotencyKey, inFlight: true },
  };
}

/** After a failed save, keep the key so a retry is safe and preserve form fields elsewhere. */
export function markValuationSaveFailed(
  state: ValuationSaveAttemptState,
): ValuationSaveAttemptState {
  return { idempotencyKey: state.idempotencyKey, inFlight: false };
}

/** After a successful database save, clear the key so the next revision gets a new attempt. */
export function markValuationSaveSucceeded(): ValuationSaveAttemptState {
  return { idempotencyKey: null, inFlight: false };
}

/**
 * Documents that failed saves must not clear worksheet field values.
 * Callers should keep the current form snapshot; this helper returns it unchanged.
 */
export function preserveValuationFormOnFailure<T>(form: T): T {
  return form;
}
