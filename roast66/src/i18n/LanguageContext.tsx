import React, {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { DEFAULT_LOCALE, SUPPORTED_LOCALES, translations } from "./translations";

const LANGUAGE_STORAGE_KEY = "roast66_locale";

function resolveDeviceLocale(): string {
  if (typeof navigator === "undefined") {
    return DEFAULT_LOCALE;
  }

  const browserLocales =
    Array.isArray(navigator.languages) && navigator.languages.length > 0
      ? navigator.languages
      : [navigator.language];

  const hasSpanishPreference = browserLocales.some(
    (value) => typeof value === "string" && value.toLowerCase().startsWith("es")
  );

  return hasSpanishPreference ? "es" : DEFAULT_LOCALE;
}

function resolveInitialLocale(): string {
  if (typeof window === "undefined") {
    return DEFAULT_LOCALE;
  }

  const savedLocale = window.localStorage.getItem(LANGUAGE_STORAGE_KEY);
  if (savedLocale && SUPPORTED_LOCALES.includes(savedLocale as "en" | "es")) {
    return savedLocale;
  }

  return resolveDeviceLocale();
}

function getNestedValue(obj: unknown, path: string): unknown {
  return path.split(".").reduce<unknown>((acc, key) => {
    if (acc && typeof acc === "object" && key in acc) {
      return (acc as Record<string, unknown>)[key];
    }
    return undefined;
  }, obj);
}

function interpolate(template: string, replacements: Record<string, string | number> = {}): string {
  return Object.entries(replacements).reduce((text, [key, value]) => {
    return text.replaceAll(`{{${key}}}`, String(value));
  }, template);
}

function fallbackTranslate(key: string, replacements: Record<string, string | number> = {}): string {
  const fallbackValue = getNestedValue(translations[DEFAULT_LOCALE], key);
  if (typeof fallbackValue !== "string") {
    return key;
  }
  return interpolate(fallbackValue, replacements);
}

export type TranslateFn = (key: string, replacements?: Record<string, string | number>) => string;

type LanguageContextValue = {
  locale: string;
  setLocale: (nextLocale: string) => void;
  t: TranslateFn;
};

const LanguageContext = createContext<LanguageContextValue>({
  locale: DEFAULT_LOCALE,
  setLocale: () => {},
  t: fallbackTranslate,
});

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState(resolveInitialLocale);

  useEffect(() => {
    if (typeof window !== "undefined") {
      window.localStorage.setItem(LANGUAGE_STORAGE_KEY, locale);
    }
    if (typeof document !== "undefined") {
      document.documentElement.lang = locale;
    }
  }, [locale]);

  const setLocale = (nextLocale: string) => {
    if (SUPPORTED_LOCALES.includes(nextLocale as "en" | "es")) {
      setLocaleState(nextLocale);
    }
  };

  const value = useMemo((): LanguageContextValue => {
    const t: TranslateFn = (key, replacements = {}) => {
      const localeValue = getNestedValue(
        translations[locale as keyof typeof translations],
        key
      );
      const fallbackValue = getNestedValue(translations[DEFAULT_LOCALE], key);
      const resolved = (localeValue ?? fallbackValue ?? key) as string | unknown;
      if (typeof resolved !== "string") {
        return key;
      }
      return interpolate(resolved, replacements);
    };

    return { locale, setLocale, t };
  }, [locale]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useI18n(): LanguageContextValue {
  return useContext(LanguageContext);
}
