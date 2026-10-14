import { useEffect, useState, type RefObject } from 'react'

/** Share of the square's width the text may use, leaving room for the border and the text's halo. */
const WidthShare = 0.85
/** Share of the square's height a line may use. */
const HeightShare = 0.75

/**
 * The largest font size (px) at which `text` fits on one line in a `width` × `height` square, given how
 * wide `text` is at 100 px. Pure, so it is tested directly.
 */
export function fitFontSize(widthAt100px: number, width: number, height: number): number {
  if (widthAt100px <= 0 || width <= 0 || height <= 0) return 0
  return Math.floor(Math.min((width * WidthShare * 100) / widthAt100px, height * HeightShare))
}

/** How wide `text` is at 100 px in the given font, or 0 when it can't be measured (no canvas, e.g. in tests). */
function measureAt100px(text: string, font: { weight: string; family: string }): number {
  const context = document.createElement('canvas').getContext('2d')
  if (!context) return 0
  context.font = `${font.weight} 100px ${font.family}`
  return context.measureText(text).width
}

/**
 * **Max** text size (FR-51): the largest size at which the longest face in the set fits a square without
 * wrapping or being cut off, recalculated when the square changes size (rotating the phone, resizing).
 * Returns `undefined` until measured, or when measuring isn't possible, so the caller can fall back.
 */
export function useFitFontSize(
  longestFace: string,
  square: RefObject<HTMLElement | null>,
): number | undefined {
  const [size, setSize] = useState<number>()

  useEffect(() => {
    const element = square.current
    if (!element || typeof ResizeObserver === 'undefined') return
    const measure = () => {
      const style = getComputedStyle(element)
      const width = measureAt100px(longestFace, { weight: '700', family: style.fontFamily })
      const fitted = fitFontSize(width, element.clientWidth, element.clientHeight)
      setSize(fitted > 0 ? fitted : undefined)
    }
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    return () => {
      observer.disconnect()
    }
  }, [longestFace, square])

  return size
}
