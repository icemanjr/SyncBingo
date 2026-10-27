import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

/**
 * jsdom has no matchMedia; the device is a phone that prefers light unless a test says otherwise
 * (`prefersDark`, `wide` for a desktop-width screen).
 */
export const media = { prefersDark: false, wide: false, listeners: new Set<() => void>() }

// Before every test, since tests that stub fetch also unstub all globals afterwards.
beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      if (query.includes('min-width')) return media.wide
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
  media.wide = false
  media.listeners.clear()
  document.documentElement.classList.remove('dark')
})
