import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  resolve: {
    // @macro/renderer is linked via "file:.." (npm creates a symlink). Without this, Node module
    // resolution can find a SECOND react/react-dom inside the renderer package's own node_modules
    // (installed there for its own tests) instead of this app's copy — two React instances end up
    // attached to the same page, each independently handling the same native event (e.g. a single
    // pointerdown firing a widget's onPress twice). Any app that consumes this package the same way
    // (the server editor in Stage 4) needs this too.
    dedupe: ["react", "react-dom"],
  },
});
