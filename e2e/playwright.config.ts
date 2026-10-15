import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests (docs/08): real browsers play against the running stack. Locally,
 * `docker compose up -d` then `npm test` here; in a container,
 * `docker compose --profile tests run --rm tests`. E2E_BASE_URL says where the site is.
 */
export default defineConfig({
  testDir: './tests',
  // A round has a 10-second countdown, so games take a while; each test is one game.
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
