import axios, { type InternalAxiosRequestConfig } from "axios";
import { API_BASE_URL } from "./config";

const instance = axios.create({
  baseURL: API_BASE_URL,
});

const PUBLIC_PATHS = ["/admin/orders", "/order", "/payments/checkout-session"];
const isPublicPost = (config: InternalAxiosRequestConfig): boolean =>
  config.method?.toLowerCase() === "post" &&
  PUBLIC_PATHS.some((p) => config.url?.includes(p));

instance.interceptors.request.use(
  (config) => {
    if (!isPublicPost(config)) {
      const token = localStorage.getItem("token");
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
      localStorage.removeItem("token");
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
