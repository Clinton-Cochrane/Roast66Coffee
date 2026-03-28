import { en } from "./strings/en";
import { esMx } from "./strings/es-MX";

export const SUPPORTED_LOCALES = ["en", "es"];

export const DEFAULT_LOCALE = "en";

/**
 * Locale bundles for `LanguageProvider`. Spanish uses Mexican Spanish copy from `es-MX.ts`
 * while the runtime locale key remains `es` for backward compatibility with stored preferences.
 */
export const translations = {
  en,
  es: esMx,
};
