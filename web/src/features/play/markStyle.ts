import { useState } from 'react'

export type MarkShape = 'circle' | 'star' | 'heart' | 'square' | 'tint'

export type TextSize = 'small' | 'medium' | 'large' | 'max'

/** How this player's marks look (FR-51), on this device only. */
export interface MarkStyle {
  shape: MarkShape
  colour: string
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
 * Mark colours: strong enough to see at 20% in both themes, and never a problem for the number, which is
 * drawn on top with a halo. "Ink" is the text colour itself: the highest contrast in either theme.
 */
export const colours: { value: string; label: string }[] = [
  { value: '#dc2626', label: 'Red' },
  { value: '#ea580c', label: 'Orange' },
  { value: '#16a34a', label: 'Green' },
  { value: '#2563eb', label: 'Blue' },
  { value: '#9333ea', label: 'Purple' },
  { value: '#db2777', label: 'Pink' },
  { value: 'var(--ink)', label: 'High contrast' },
]

export const textSizes: { value: TextSize; label: string; className: string }[] = [
  { value: 'small', label: 'Small', className: 'text-base' },
  { value: 'medium', label: 'Medium', className: 'text-xl' },
  { value: 'large', label: 'Large', className: 'text-3xl' },
  { value: 'max', label: 'Max', className: 'text-4xl' },
]

export const defaultMarkStyle: MarkStyle = {
  shape: 'circle',
  colour: '#2563eb',
  opacity: 0.45,
  textSize: 'medium',
}

const storageKey = 'syncbingo:mark-style'
const clampOpacity = (value: number) => Math.min(0.8, Math.max(0.2, value))

export function loadMarkStyle(): MarkStyle {
  try {
    const saved = JSON.parse(
      localStorage.getItem(storageKey) ?? 'null',
    ) as Partial<MarkStyle> | null
    if (!saved) return defaultMarkStyle
    const shape = shapes.find((s) => s.value === saved.shape)?.value
    const colour = colours.find((c) => c.value === saved.colour)?.value
    const textSize = textSizes.find((t) => t.value === saved.textSize)?.value
    return {
      shape: shape ?? defaultMarkStyle.shape,
      colour: colour ?? defaultMarkStyle.colour,
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
