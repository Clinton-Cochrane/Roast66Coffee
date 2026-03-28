/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "dist",
  },
  test: {
    environment: "jsdom",
    globals: false,
    setupFiles: "./src/setupTests.ts",
    css: true,
    restoreMocks: true,
  },
});
