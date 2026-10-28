import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { loadEnv, type Plugin } from 'vite'
import { defineConfig } from 'vitest/config'
import { adsTxt } from './src/app/adsTxt.ts'

/**
 * With an AdSense publisher id set (ADR 0012), the page carries Google's site-verification tag and the build
 * writes `ads.txt`, which AdSense's site review and ad buyers look for. A malformed id fails the build.
 * Without one, neither exists.
 */
function adSense(client: string | undefined): Plugin {
  return {
    name: 'syncbingo-adsense',
    transformIndexHtml: () =>
      client
        ? [
            {
              tag: 'meta',
              attrs: { name: 'google-adsense-account', content: client },
              injectTo: 'head',
            },
          ]
        : [],
    generateBundle() {
      if (client) this.emitFile({ type: 'asset', fileName: 'ads.txt', source: adsTxt(client) })
    },
  }
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [
    react(),
    tailwindcss(),
    adSense(loadEnv(mode, process.cwd(), 'VITE_').VITE_ADSENSE_CLIENT),
  ],
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
}))
