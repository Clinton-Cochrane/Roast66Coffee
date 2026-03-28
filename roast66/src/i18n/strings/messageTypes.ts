/**
 * Widens literal string types produced by `as const` so translated locale
 * bundles (e.g. es-MX) can use different copy while staying structurally aligned
 * with English.
 */
export type WidenStrings<T> = T extends string
  ? string
  : T extends object
    ? { [K in keyof T]: WidenStrings<T[K]> }
    : T;
