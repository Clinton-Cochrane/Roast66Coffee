import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import PropTypes from "prop-types";
import { DEFAULT_LOCALE, SUPPORTED_LOCALES, translations } from "./translations";

const LANGUAGE_STORAGE_KEY = "roast66_locale";

function resolveDeviceLocale() {
  if (typeof navigator === "undefined") {
    return DEFAULT_LOCALE;
  }

  const browserLocales = Array.isArray(navigator.languages) && navigator.languages.length > 0
    ? navigator.languages
    : [navigator.language];

  const hasSpanishPreference = browserLocales.some(
    (value) => typeof value === "string" && value.toLowerCase().startsWith("es")
  );

  return hasSpanishPreference ? "es" : DEFAULT_LOCALE;
}

function resolveInitialLocale() {
  if (typeof window === "undefined") {
    return DEFAULT_LOCALE;
  }

  const savedLocale = window.localStorage.getItem(LANGUAGE_STORAGE_KEY);
  if (SUPPORTED_LOCALES.includes(savedLocale)) {
    return savedLocale;
  }

  return resolveDeviceLocale();
}

function getNestedValue(obj, path) {
  return path.split(".").reduce((acc, key) => (acc && acc[key] != null ? acc[key] : undefined), obj);
}

function interpolate(template, replacements = {}) {
  return Object.entries(replacements).reduce((text, [key, value]) => {
    return text.replaceAll(`{{${key}}}`, String(value));
  }, template);
}

function fallbackTranslate(key, replacements = {}) {
  const fallbackValue = getNestedValue(translations[DEFAULT_LOCALE], key);
  if (typeof fallbackValue !== "string") {
    return key;
  }
  return interpolate(fallbackValue, replacements);
}

const LanguageContext = createContext({
  locale: DEFAULT_LOCALE,
  setLocale: () => {},
  t: fallbackTranslate,
});

export function LanguageProvider({ children }) {
  const [locale, setLocaleState] = useState(resolveInitialLocale);

  useEffect(() => {
    if (typeof window !== "undefined") {
      window.localStorage.setItem(LANGUAGE_STORAGE_KEY, locale);
    }
    if (typeof document !== "undefined") {
      document.documentElement.lang = locale;
    }
  }, [locale]);

  const setLocale = (nextLocale) => {
    if (SUPPORTED_LOCALES.includes(nextLocale)) {
      setLocaleState(nextLocale);
    }
  };

  const value = useMemo(() => {
    const t = (key, replacements = {}) => {
      const localeValue = getNestedValue(translations[locale], key);
      const fallbackValue = getNestedValue(translations[DEFAULT_LOCALE], key);
      const resolved = localeValue ?? fallbackValue ?? key;
      if (typeof resolved !== "string") {
        return key;
      }
      return interpolate(resolved, replacements);
    };

    return { locale, setLocale, t };
  }, [locale]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

LanguageProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

export function useI18n() {
  return useContext(LanguageContext);
}
