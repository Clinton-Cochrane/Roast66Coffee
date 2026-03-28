# UI strings

- [`en.ts`](./en.ts) — English copy (source of truth for keys).
- [`es-MX.ts`](./es-MX.ts) — Spanish (Mexico) copy; keys must match `Messages` from `en.ts`.

Bundles are composed in [`../translations.ts`](../translations.ts). The runtime locale key remains `es` for stored preferences; content is Mexican Spanish.

Add or change copy in these files only—do not hardcode user-visible text in components.
