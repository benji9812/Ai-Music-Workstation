import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    chunkSizeWarningLimit: 750,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) return;
          if (id.includes("tone")) return "tone";
          if (id.includes("svguitar")) return "svguitar";
          if (id.includes("react")) return "react";
          return "vendor";
        },
      },
    },
  },
})
