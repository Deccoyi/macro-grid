import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: {
    // Same reason as vite.config.ts: @macro/renderer is a symlinked "file:.." package with its own
    // node_modules, and two React copies in one test would break every hook.
    dedupe: ["react", "react-dom"],
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./test/setup.ts"],
    globals: false,
  },
});
