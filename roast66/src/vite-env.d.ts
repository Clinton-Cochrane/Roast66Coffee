/// <reference types="vite/client" />

declare module "*.png" {
  const src: string;
  export default src;
}

/** PWA install prompt (Chromium); not in all TS lib.dom versions */
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
}

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_ENABLE_STRIPE_CHECKOUT?: string;
  readonly VITE_VAPID_PUBLIC_KEY?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
