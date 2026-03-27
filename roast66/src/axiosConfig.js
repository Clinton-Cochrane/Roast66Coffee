import axios from 'axios';
import { API_BASE_URL } from './config';

const instance = axios.create({
  baseURL: API_BASE_URL,
});

// Add a request interceptor to include the token for authenticated endpoints only.
// Public endpoints (e.g. order placement) must not receive the token—invalid/expired
// tokens cause JWT validation to fail and can trigger 405 via the exception handler.
const PUBLIC_PATHS = ['/admin/orders', '/order', '/payments/checkout-session'];
const isPublicPost = (config) =>
  config.method?.toLowerCase() === 'post' &&
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
  (error) => Promise.reject(error)
);

instance.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status;
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
