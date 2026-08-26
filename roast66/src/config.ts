/**
 * Centralized app configuration. Override via environment variables.
 * @see roast66/.env.example for local development
 */
const DEFAULT_API_BASE_URL = "https://roast66coffee.onrender.com/api";

export const API_BASE_URL =
  import.meta.env.VITE_API_URL || DEFAULT_API_BASE_URL;

const staticMenuSetting = import.meta.env.VITE_USE_STATIC_MENU?.trim().toLowerCase();
const isLocalBrowser =
  typeof window !== "undefined" &&
  ["localhost", "127.0.0.1", "[::1]"].includes(window.location.hostname);

export const USE_STATIC_MENU =
  staticMenuSetting === "true" ||
  (staticMenuSetting !== "false" && (import.meta.env.DEV || isLocalBrowser));
