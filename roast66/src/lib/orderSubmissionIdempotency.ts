/**
 * Keeps one idempotency key for one normalized UI payload in sessionStorage.
 * Network retries in the same tab therefore replay the server result, while an
 * intentional payload change receives a fresh key. Memory is a fallback for
 * browsers that deny storage access.
 */
const PENDING_ORDER_SUBMISSION_KEY = "roast66.pending-order-submission";

type PendingOrderSubmission = {
  idempotencyKey: string;
  payloadSignature: string;
};

let memoryPendingSubmission: PendingOrderSubmission | null = null;

const generateIdempotencyKey = (): string =>
  window.crypto?.randomUUID?.() ||
  `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;

const readPendingSubmission = (): PendingOrderSubmission | null => {
  try {
    const raw = sessionStorage.getItem(PENDING_ORDER_SUBMISSION_KEY);
    if (!raw) {
      memoryPendingSubmission = null;
      return null;
    }
    const parsed = JSON.parse(raw) as Partial<PendingOrderSubmission>;
    if (
      typeof parsed.idempotencyKey !== "string" ||
      typeof parsed.payloadSignature !== "string"
    ) {
      return memoryPendingSubmission;
    }
    memoryPendingSubmission = parsed as PendingOrderSubmission;
    return memoryPendingSubmission;
  } catch {
    return memoryPendingSubmission;
  }
};

/** Reuses the pending key only when JSON-stable customer intent is unchanged. */
export const getOrCreateOrderIdempotencyKey = (payload: unknown): string => {
  const payloadSignature = JSON.stringify(payload);
  const pending = readPendingSubmission();
  if (pending?.payloadSignature === payloadSignature) {
    return pending.idempotencyKey;
  }

  const idempotencyKey = generateIdempotencyKey();
  memoryPendingSubmission = { idempotencyKey, payloadSignature };
  try {
    sessionStorage.setItem(
      PENDING_ORDER_SUBMISSION_KEY,
      JSON.stringify({ idempotencyKey, payloadSignature } satisfies PendingOrderSubmission)
    );
  } catch {
    // The in-memory key still protects this attempt when storage is unavailable.
  }
  return idempotencyKey;
};

/** Clears only the completed request, preserving a newer in-flight submission. */
export const clearOrderIdempotencyKey = (idempotencyKey: string): void => {
  const pending = readPendingSubmission();
  if (pending?.idempotencyKey !== idempotencyKey) return;
  memoryPendingSubmission = null;
  try {
    sessionStorage.removeItem(PENDING_ORDER_SUBMISSION_KEY);
  } catch {
    // Storage may be unavailable in hardened browser contexts.
  }
};

export { PENDING_ORDER_SUBMISSION_KEY };
