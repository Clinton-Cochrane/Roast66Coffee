import axios, { type InternalAxiosRequestConfig } from "axios";
import { API_BASE_URL, USE_STATIC_MENU } from "./config";
import { clearAdminSession, getAdminToken } from "./authSession";

/**
 * Shared API transport. It redirects local menu reads to the bundled snapshot
 * only in static-menu mode, attaches staff credentials everywhere except the
 * explicitly public POST routes, and centralizes expired-session handling.
 */
const instance = axios.create({
  baseURL: API_BASE_URL,
});

const PUBLIC_PATHS = ["/order", "/payments/checkout-session"];
const isPublicPost = (config: InternalAxiosRequestConfig): boolean => {
  const path = config.url?.split(/[?#]/, 1)[0];
  return config.method?.toLowerCase() === "post" && PUBLIC_PATHS.includes(path ?? "");
};

instance.interceptors.request.use(
  (config) => {
    if (
      USE_STATIC_MENU &&
      config.method?.toLowerCase() === "get" &&
      config.url === "/menu"
    ) {
      config.baseURL = undefined;
      config.url = `${import.meta.env.BASE_URL}data/menu.json`;
    }

    // Never send a staff JWT to customer submission or provider checkout routes.
    if (!isPublicPost(config)) {
      const token = getAdminToken();
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
    }
    return config;
  },
  (error: unknown) => Promise.reject(error)
);

instance.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    const status =
      error &&
      typeof error === "object" &&
      "response" in error &&
      error.response &&
      typeof error.response === "object" &&
      "status" in error.response
        ? (error.response as { status: number }).status
        : undefined;
    if (status === 401 && typeof window !== "undefined") {
      // A rejected staff request invalidates the shared browser session. Keep the
      // cash workflow on /cash; its gate renders the appropriate sign-in view.
      clearAdminSession();
      const pathname = window.location.pathname || "/";
      const loginPath = pathname.startsWith("/cash") ? "/cash" : "/admin";
      if (pathname !== loginPath) {
        window.location.assign(loginPath);
      }
    }
    return Promise.reject(error);
  }
);

export default instance;
