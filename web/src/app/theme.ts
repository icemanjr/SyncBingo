import { useEffect, useState } from 'react'

/** The player's choice (FR-50): follow the device, or always light, or always dark. */
export type ThemePreference = 'system' | 'light' | 'dark'

const storageKey = 'syncbingo:theme'
const darkQuery = '(prefers-color-scheme: dark)'

export function loadThemePreference(): ThemePreference {
  try {
    const saved = localStorage.getItem(storageKey)
    return saved === 'light' || saved === 'dark' ? saved : 'system'
  } catch {
    return 'system' // storage blocked: follow the device
  }
}

function saveThemePreference(preference: ThemePreference): void {
  try {
    if (preference === 'system') localStorage.removeItem(storageKey)
    else localStorage.setItem(storageKey, preference)
  } catch {
    // Not remembered; it still applies on this page.
  }
}

/** Sets or clears the `dark` class on <html> for this preference. */
export function applyTheme(preference: ThemePreference): void {
  const dark =
    preference === 'dark' || (preference === 'system' && window.matchMedia(darkQuery).matches)
  document.documentElement.classList.toggle('dark', dark)
}

/** The theme preference, remembered on this device; "system" also follows the device changing its own setting. */
export function useTheme(): [ThemePreference, (preference: ThemePreference) => void] {
  const [preference, setPreference] = useState(loadThemePreference)

  useEffect(() => {
    applyTheme(preference)
    if (preference !== 'system') return
    const media = window.matchMedia(darkQuery)
    const follow = () => {
      applyTheme('system')
    }
    media.addEventListener('change', follow)
    return () => {
      media.removeEventListener('change', follow)
    }
  }, [preference])

  return [
    preference,
    (next) => {
      saveThemePreference(next)
      setPreference(next)
    },
  ]
}
