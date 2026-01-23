import { defineConfig } from "vite";

export default defineConfig({
  // Ensures built assets use relative URLs (important when served from devices/subfolders)
  base: "./",
  server: {
    port: 5173,
    strictPort: true
    // If you need to bind to all interfaces: host: true
    // If you want specific HMR options, you can add: hmr: { port: 5173 }
  },
  preview: {
    // Optional: keep preview on a known port; default is 4173
    port: 5173,
    strictPort: true
  },
  build: {
    outDir: "dist"
  }
});