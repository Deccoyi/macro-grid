import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  base: "/editor/",
  plugins: [react()],
  resolve: {
    // @macro/renderer is linked via "file:.." (npm symlink). Without this, module resolution can
    // find a SECOND react/react-dom inside the renderer package's own node_modules instead of this
    // app's copy, and two React instances end up on the page (see docs/development.md, Pitfalls).
    dedupe: ["react", "react-dom"],
  },
  build: {
    // Copied into the Host's wwwroot at build time (see docs/development.md) and served
    // same-origin, so the packaged app never needs the dev proxy below.
    outDir: "dist",
  },
  server: {
    port: 5190,
    proxy: {
      // Talk to the real running server for API/WS during development instead of enabling CORS on it.
      "/api": "http://localhost:9820",
      "/ws": { target: "ws://localhost:9820", ws: true },
    },
  },
});
