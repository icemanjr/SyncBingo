import { useState } from 'react'

export type MarkShape = 'circle' | 'star' | 'heart' | 'square' | 'tint'

export type TextSize = 'small' | 'medium' | 'large' | 'max'

/** How this player's marks look (FR-51), on this device only. */
export interface MarkStyle {
  shape: MarkShape
  color: string
  /** 0.2 to 0.8 */
  opacity: number
  /** How big the text in the squares is (a number, or a word with custom sets); Max still fits two digits on a small phone. */
  textSize: TextSize
}

export const shapes: { value: MarkShape; label: string; symbol: string }[] = [
  { value: 'circle', label: 'Circle', symbol: '●' },
  { value: 'star', label: 'Star', symbol: '★' },
  { value: 'heart', label: 'Heart', symbol: '♥' },
  { value: 'square', label: 'Square', symbol: '■' },
  { value: 'tint', label: 'Whole square', symbol: '▢' },
]

/**
 * Mark colors: strong enough to see at 20% in both themes, and never a problem for the number, which is
 * drawn on top with a halo. "Ink" is the text color itself: the highest contrast in either theme.
 */
export const colors: { value: string; label: string }[] = [
  { value: '#dc2626', label: 'Red' },
  { value: '#ea580c', label: 'Orange' },
  { value: '#16a34a', label: 'Green' },
  { value: '#2563eb', label: 'Blue' },
  { value: '#9333ea', label: 'Purple' },
  { value: '#db2777', label: 'Pink' },
  { value: 'var(--ink)', label: 'High contrast' },
]

/**
 * Text sizes (FR-51): each starts at `start` px and may shrink to fit a square, never below `min` (6, 10 and
 * 14 pt, in 4 pt steps). Max is fitted to the set's hardest face and never goes below 18 pt. The minimums
 * were lowered by 2 pt (2026-10-19) so custom sets' words fit more often on a phone's 50–56 px squares.
 */
export const textSizes: {
  value: TextSize
  label: string
  className: string
  start: number
  min: number
}[] = [
  { value: 'small', label: 'Small', className: 'text-base', start: 16, min: (6 * 4) / 3 },
  { value: 'medium', label: 'Medium', className: 'text-xl', start: 20, min: (10 * 4) / 3 },
  { value: 'large', label: 'Large', className: 'text-3xl', start: 30, min: (14 * 4) / 3 },
  { value: 'max', label: 'Max', className: 'text-4xl', start: 36, min: (18 * 4) / 3 },
]

export const defaultMarkStyle: MarkStyle = {
  shape: 'circle',
  color: '#2563eb',
  opacity: 0.45,
  textSize: 'medium',
}

const storageKey = 'syncbingo:mark-style'
const clampOpacity = (value: number) => Math.min(0.8, Math.max(0.2, value))

export function loadMarkStyle(): MarkStyle {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) ?? 'null') as
      (Partial<MarkStyle> & { color?: string }) | null
    if (!saved) return defaultMarkStyle
    const shape = shapes.find((s) => s.value === saved.shape)?.value
    const color = colors.find((c) => c.value === saved.color)?.value
    const textSize = textSizes.find((t) => t.value === saved.textSize)?.value
    return {
      shape: shape ?? defaultMarkStyle.shape,
      color: color ?? defaultMarkStyle.color,
      opacity:
        typeof saved.opacity === 'number' ? clampOpacity(saved.opacity) : defaultMarkStyle.opacity,
      textSize: textSize ?? defaultMarkStyle.textSize,
    }
  } catch {
    return defaultMarkStyle
  }
}

/** The player's mark style, remembered on this device; never sent to the server. */
export function useMarkStyle(): [MarkStyle, (style: MarkStyle) => void] {
  const [style, setStyle] = useState(loadMarkStyle)
  return [
    style,
    (next) => {
      const clamped = { ...next, opacity: clampOpacity(next.opacity) }
      try {
        localStorage.setItem(storageKey, JSON.stringify(clamped))
      } catch {
        // Not remembered, but applies now.
      }
      setStyle(clamped)
    },
  ]
}
