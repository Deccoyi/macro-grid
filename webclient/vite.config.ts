import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  base: "/deck/",
  plugins: [react()],
  resolve: {
    // @macro/renderer is linked via "file:.." — without this a second react/react-dom can end up
    // resolving from inside the renderer package's own node_modules. See editor/vite.config.ts, same fix.
    dedupe: ["react", "react-dom"],
  },
  build: {
    // Copied into the Host's wwwroot/deck at build time and served same-origin — see docs/agent-notes.md.
    outDir: "dist",
  },
  server: {
    port: 5192,
    proxy: {
      "/api": "http://localhost:9820",
      "/ws": { target: "ws://localhost:9820", ws: true },
    },
  },
});
