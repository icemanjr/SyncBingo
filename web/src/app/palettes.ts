import { useEffect, useState } from 'react'
import type { Schemas } from '../api/client'

/**
 * Color themes (FR-50a): the host picks one for the game, and a player may pick another on their own device.
 * Colors only, so nothing extra is downloaded. Classic is the design in index.css; the others change the
 * page, surfaces, accent, last ball and group colors, in light and in dark, and leave text colors and the
 * B-I-N-G-O letters alone, so every palette keeps the contrast checked in palettes.test.ts.
 */
export type Palette = Schemas['Theme']

/** The color tokens a palette may change, as index.css names them (without the leading "--"). */
export type PaletteToken =
  | 'page'
  | 'surface'
  | 'line-soft'
  | 'accent'
  | 'accent-strong'
  | 'accent-ink'
  | 'accent-text'
  | 'accent-soft'
  | 'group'
  | 'ball'
  | 'ball-ink'

type Colors = Record<PaletteToken, string>

export const palettes: { value: Palette; label: string }[] = [
  { value: 'Classic', label: 'Classic' },
  { value: 'Ocean', label: 'Ocean' },
  { value: 'Forest', label: 'Forest' },
  { value: 'Halloween', label: 'Halloween' },
  { value: 'Winter', label: 'Winter' },
]

/** Each palette's colors, light and dark; Classic has none here, since index.css is Classic. */
export const paletteColors: Record<Exclude<Palette, 'Classic'>, { light: Colors; dark: Colors }> = {
  Ocean: {
    light: {
      page: '#f1f6fa',
      surface: '#ffffff',
      'line-soft': '#dbe7f0',
      accent: '#0b6e99',
      'accent-strong': '#075a7e',
      'accent-ink': '#ffffff',
      'accent-text': '#075a7e',
      'accent-soft': '#e0f0f8',
      group: '#f3f8fb',
      ball: '#4fd1c5',
      'ball-ink': '#0f2a33',
    },
    dark: {
      page: '#0b1a24',
      surface: '#13283a',
      'line-soft': '#1f3a4a',
      accent: '#5cc8f0',
      'accent-strong': '#8fdaf5',
      'accent-ink': '#0b1a24',
      'accent-text': '#8fdaf5',
      'accent-soft': '#123447',
      group: '#14293a',
      ball: '#4fd1c5',
      'ball-ink': '#0f2a33',
    },
  },
  Forest: {
    light: {
      page: '#f2f7f2',
      surface: '#ffffff',
      'line-soft': '#dce9dc',
      accent: '#2f7d32',
      'accent-strong': '#1f5f23',
      'accent-ink': '#ffffff',
      'accent-text': '#1f5f23',
      'accent-soft': '#e3f1e3',
      group: '#f4f9f4',
      ball: '#f2c14e',
      'ball-ink': '#1c2a1c',
    },
    dark: {
      page: '#0f1a10',
      surface: '#1a2a1b',
      'line-soft': '#2c402d',
      accent: '#7cc47f',
      'accent-strong': '#a5d9a7',
      'accent-ink': '#0f1a10',
      'accent-text': '#a5d9a7',
      'accent-soft': '#213a23',
      group: '#1e301f',
      ball: '#f2c14e',
      'ball-ink': '#1c2a1c',
    },
  },
  Halloween: {
    light: {
      page: '#fbf4ec',
      surface: '#ffffff',
      'line-soft': '#efdfcc',
      accent: '#a8470a',
      'accent-strong': '#8a3c05',
      'accent-ink': '#ffffff',
      'accent-text': '#8a3c05',
      'accent-soft': '#fde8d4',
      group: '#fcf6ef',
      ball: '#ff8c1a',
      'ball-ink': '#1a1023',
    },
    dark: {
      page: '#120d18',
      surface: '#1f1729',
      'line-soft': '#3a2c45',
      accent: '#ff9a3c',
      'accent-strong': '#ffb877',
      'accent-ink': '#120d18',
      'accent-text': '#ffb877',
      'accent-soft': '#3d2614',
      group: '#261c31',
      ball: '#ff8c1a',
      'ball-ink': '#1a1023',
    },
  },
  Winter: {
    light: {
      page: '#f3f6fb',
      surface: '#ffffff',
      'line-soft': '#dfe6f2',
      accent: '#3157a8',
      'accent-strong': '#22407f',
      'accent-ink': '#ffffff',
      'accent-text': '#22407f',
      'accent-soft': '#e4ebf8',
      group: '#f5f8fc',
      ball: '#9fd3ff',
      'ball-ink': '#10203a',
    },
    dark: {
      page: '#0d1422',
      surface: '#182235',
      'line-soft': '#2a3850',
      accent: '#8fb4ff',
      'accent-strong': '#b5ceff',
      'accent-ink': '#0d1422',
      'accent-text': '#b5ceff',
      'accent-soft': '#1f2f4f',
      group: '#1c2840',
      ball: '#9fd3ff',
      'ball-ink': '#10203a',
    },
  },
}

const block = (colors: Colors) =>
  Object.entries(colors)
    .map(([token, value]) => `--${token}:${value};`)
    .join('')

/**
 * The palettes as CSS, keyed by `data-palette` on <html>. The dark rule is more specific than both the light
 * rule and index.css's `.dark`, so each palette's dark colors win in dark mode.
 */
export function paletteCss(): string {
  return Object.entries(paletteColors)
    .map(
      ([name, { light, dark }]) =>
        `:root[data-palette="${name}"]{${block(light)}}:root.dark[data-palette="${name}"]{${block(dark)}}`,
    )
    .join('\n')
}

/** Adds the palettes' CSS to the page once, before the first render. */
export function installPalettes(): void {
  if (document.getElementById('palettes')) return
  const style = document.createElement('style')
  style.id = 'palettes'
  style.textContent = paletteCss()
  document.head.append(style)
}

/** Shows a palette (Classic: none) while the calling screen is open, then goes back to Classic. */
export function usePalette(palette: Palette | undefined): void {
  useEffect(() => {
    const root = document.documentElement
    if (palette && palette !== 'Classic') root.dataset.palette = palette
    else delete root.dataset.palette
    return () => {
      delete root.dataset.palette
    }
  }, [palette])
}

/** A player's own choice on this device: the game's theme (the default), or a palette of their own. */
export type PaletteChoice = 'game' | Palette

const storageKey = 'syncbingo:palette'

export function loadPaletteChoice(): PaletteChoice {
  try {
    const saved = localStorage.getItem(storageKey)
    return palettes.some((p) => p.value === saved) ? (saved as Palette) : 'game'
  } catch {
    return 'game' // storage blocked: follow the game
  }
}

/** The player's palette choice, remembered on this device (for every game, like the mark style). */
export function usePaletteChoice(): [PaletteChoice, (choice: PaletteChoice) => void] {
  const [choice, setChoice] = useState(loadPaletteChoice)
  return [
    choice,
    (next) => {
      try {
        if (next === 'game') localStorage.removeItem(storageKey)
        else localStorage.setItem(storageKey, next)
      } catch {
        // Not remembered; it still applies on this page.
      }
      setChoice(next)
    },
  ]
}
