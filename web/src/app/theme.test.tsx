import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { media } from '../test/setup'
import { ThemeToggle } from './ThemeToggle'
import { applyTheme, loadThemePreference } from './theme'

const isDark = () => document.documentElement.classList.contains('dark')

describe('the theme (FR-50)', () => {
  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('follows the device by default', () => {
    media.prefersDark = true

    applyTheme(loadThemePreference())

    expect(loadThemePreference()).toBe('system')
    expect(isDark()).toBe(true)
  })

  it('switches to dark and remembers it on this device', async () => {
    render(<ThemeToggle />)

    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(isDark()).toBe(true)
    expect(screen.getByRole('radio', { name: 'Dark' })).toHaveAttribute('aria-checked', 'true')
    expect(loadThemePreference()).toBe('dark')
  })

  it('stays light when chosen, even on a dark device', async () => {
    media.prefersDark = true
    render(<ThemeToggle />)

    await userEvent.click(screen.getByRole('radio', { name: 'Light' }))

    expect(isDark()).toBe(false)
  })

  it('on "System", follows the device changing its own setting', () => {
    render(<ThemeToggle />)

    media.prefersDark = true
    media.listeners.forEach((listener) => {
      listener()
    })

    expect(isDark()).toBe(true)
  })

  it('works when storage is blocked', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    render(<ThemeToggle />)

    await userEvent.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(isDark()).toBe(true)
  })
})
