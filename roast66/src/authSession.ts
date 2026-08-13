import { useSyncExternalStore } from "react";

const TOKEN_KEY = "token";
const SESSION_EVENT = "roast66:adminSessionChanged";

function decodeExpiry(token: string): number | null {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const decoded = JSON.parse(atob(normalized)) as { exp?: unknown };
    return typeof decoded.exp === "number" ? decoded.exp * 1000 : null;
  } catch {
    return null;
  }
}

export function getAdminToken(): string | null {
  if (typeof window === "undefined") return null;
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) return null;
  const expiry = decodeExpiry(token);
  if (expiry === null || expiry <= Date.now()) {
    localStorage.removeItem(TOKEN_KEY);
    return null;
  }
  return token;
}

export function setAdminSession(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
  window.dispatchEvent(new Event(SESSION_EVENT));
}

export function clearAdminSession(): void {
  localStorage.removeItem(TOKEN_KEY);
  window.dispatchEvent(new Event(SESSION_EVENT));
}

function subscribe(callback: () => void): () => void {
  window.addEventListener(SESSION_EVENT, callback);
  window.addEventListener("storage", callback);
  const interval = window.setInterval(callback, 30_000);
  return () => {
    window.removeEventListener(SESSION_EVENT, callback);
    window.removeEventListener("storage", callback);
    window.clearInterval(interval);
  };
}

export function useAdminToken(): string | null {
  return useSyncExternalStore(subscribe, getAdminToken, () => null);
}
