import { useEffect, useState, type RefObject } from 'react'

/** How wide `text` is when drawn at 100 px. In the browser a canvas measures it; tests pass a fake. */
export type Measure = (text: string) => number

/** The room a square gives its text, in px. */
export interface Box {
  width: number
  height: number
}

/** How a face is drawn: its size (px), its lines, and whether its end was cut off with "…". */
export interface Fitted {
  size: number
  lines: string[]
  truncated: boolean
}

/** 1 pt = 4/3 px. The minimums are set in points (FR-51). */
export const pt = (points: number) => (points * 4) / 3

/** Line height as a share of the font size. */
export const LineHeight = 1.15

/** Share of a square's width and height the text may use, leaving room for the border and the halo. */
export const BoxShare = 0.85

const Ellipsis = '…'

const words = (text: string) => text.trim().split(/\s+/).filter(Boolean)

/** Lines for `text` at `size`, breaking only at spaces; `null` if a word doesn't fit the width on its own. */
export function wrap(text: string, measure: Measure, size: number, width: number): string[] | null {
  const scale = size / 100
  const lines: string[] = []
  let line = ''
  for (const word of words(text)) {
    if (measure(word) * scale > width) return null
    const candidate = line ? `${line} ${word}` : word
    if (measure(candidate) * scale <= width) {
      line = candidate
    } else {
      lines.push(line)
      line = word
    }
  }

  if (line) lines.push(line)
  return lines
}

/** The lines if `text` fits the box at `size` (whole words, within the height), else `null`. */
export function fitsAt(text: string, measure: Measure, size: number, box: Box): string[] | null {
  const lines = wrap(text, measure, size, box.width)
  return lines && lines.length * size * LineHeight <= box.height ? lines : null
}

/** The largest whole-px size in [min, max] at which `text` fits, or `undefined`. */
function largestFit(
  text: string,
  measure: Measure,
  box: Box,
  min: number,
  max: number,
): number | undefined {
  let low = Math.ceil(min)
  let high = Math.floor(max)
  let best: number | undefined
  while (low <= high) {
    const mid = Math.floor((low + high) / 2)
    if (fitsAt(text, measure, mid, box)) {
      best = mid
      low = mid + 1
    } else {
      high = mid - 1
    }
  }

  return best
}

/**
 * Cuts `text` to what fits at `size` and ends it with "…": whole words while they fit, then as much of the
 * next word as fits beside the ellipsis. Only a single word too wide for the square is cut inside itself.
 */
export function truncate(text: string, measure: Measure, size: number, box: Box): string[] {
  const scale = size / 100
  const fitsWidth = (line: string) => measure(line) * scale <= box.width
  const maxLines = Math.max(1, Math.floor(box.height / (size * LineHeight)))
  const lines: string[] = []
  let line = ''
  for (const word of words(text)) {
    const candidate = line ? `${line} ${word}` : word
    if (fitsWidth(candidate)) {
      line = candidate
      continue
    }

    if (line && lines.length + 1 < maxLines && fitsWidth(word)) {
      lines.push(line)
      line = word
      continue
    }

    // Out of room: finish this line with as much as fits before "…".
    let cut = candidate
    while (cut.length > 0 && !fitsWidth(cut + Ellipsis)) cut = cut.slice(0, -1)
    lines.push(cut.trimEnd() + Ellipsis)
    return lines
  }

  if (line) lines.push(line)
  return lines
}

/**
 * A face at a fixed text size (Small, Medium, Large; FR-51): at `start` if it fits, else smaller until it
 * does, but never below `min`; at `min`, cut off with "…" if it still doesn't fit.
 */
export function fitText(
  text: string,
  measure: Measure,
  box: Box,
  start: number,
  min: number,
): Fitted {
  const size = largestFit(text, measure, box, min, start)
  if (size !== undefined) {
    return { size, lines: fitsAt(text, measure, size, box) ?? [text], truncated: false }
  }

  return { size: min, lines: truncate(text, measure, min, box), truncated: true }
}

/**
 * **Max** (FR-51): one size for every square, the largest at which the set's hardest face fits whole (not
 * always the longest: one long word can be harder than a short sentence), never below `floor`.
 */
export function maxSizeForSet(faces: string[], measure: Measure, box: Box, floor: number): number {
  let size = Math.floor(box.height)
  for (const face of faces) {
    size = Math.min(size, largestFit(face, measure, box, 1, size) ?? 0)
  }

  return Math.max(size, floor)
}

/** A canvas measurer in the given font, or `undefined` where text can't be measured (tests). */
function canvasMeasure(family: string): Measure | undefined {
  const context = document.createElement('canvas').getContext('2d')
  if (!context) return undefined
  context.font = `700 100px ${family}`
  const cache = new Map<string, number>()
  return (text) => {
    let width = cache.get(text)
    if (width === undefined) {
      width = context.measureText(text).width
      cache.set(text, width)
    }

    return width
  }
}

/**
 * The room in one square (the card's squares are all the same size) and a measurer in its font, updated
 * when the square resizes (rotating the phone, resizing the window). `undefined` until measured, or where
 * measuring isn't possible, so callers fall back to plain CSS sizes.
 */
export function useSquareMetrics(
  square: RefObject<HTMLElement | null>,
): { box: Box; measure: Measure } | undefined {
  const [metrics, setMetrics] = useState<{ box: Box; measure: Measure }>()

  useEffect(() => {
    const element = square.current
    if (!element || typeof ResizeObserver === 'undefined') return
    const measure = canvasMeasure(getComputedStyle(element).fontFamily)
    if (!measure) return
    const update = () => {
      setMetrics({
        box: { width: element.clientWidth * BoxShare, height: element.clientHeight * BoxShare },
        measure,
      })
    }
    const observer = new ResizeObserver(update)
    observer.observe(element)
    return () => {
      observer.disconnect()
    }
  }, [square])

  return metrics
}
