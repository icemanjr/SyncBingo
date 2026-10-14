import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

/** jsdom has no matchMedia; the device prefers light unless a test says otherwise (`prefersDark`). */
export const media = { prefersDark: false, listeners: new Set<() => void>() }

// Before every test, since tests that stub fetch also unstub all globals afterwards.
beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      return query.includes('dark') && media.prefersDark
    },
    media: query,
    addEventListener: (_: string, listener: () => void) => media.listeners.add(listener),
    removeEventListener: (_: string, listener: () => void) => media.listeners.delete(listener),
  }))
})

afterEach(() => {
  cleanup()
  media.prefersDark = false
  media.listeners.clear()
  document.documentElement.classList.remove('dark')
})
