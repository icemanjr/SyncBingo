import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // In development the API runs on its own port (docker compose: 5080); the app calls it on its own origin.
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
      '/hubs': { target: 'ws://localhost:5080', ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
