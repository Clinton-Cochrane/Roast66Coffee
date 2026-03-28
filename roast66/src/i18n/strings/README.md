# UI strings

- [`en.ts`](./en.ts) — English copy (source of truth for keys).
- [`es-MX.ts`](./es-MX.ts) — Spanish (Mexico) copy; keys must match `Messages` from `en.ts`.
- [`messageTypes.ts`](./messageTypes.ts) — `WidenStrings` so `Messages` is not tied to English string literals (avoids TS2322 when translations differ).

Bundles are composed in [`../translations.ts`](../translations.ts). The runtime locale key remains `es` for stored preferences; content is Mexican Spanish.

Add or change copy in these files only—do not hardcode user-visible text in components.
