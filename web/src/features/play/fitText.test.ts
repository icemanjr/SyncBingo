import { describe, expect, it } from 'vitest'
import { fitText, maxSizeForSet, pt, truncate, wrap } from './fitText'

/** A monospace stand-in: every character is 60 px wide at 100 px. */
const mono = (text: string) => text.length * 60
const square = { width: 68, height: 68 } // an 80 px phone square, less the border and halo

describe('fitting text in a square (FR-51)', () => {
  it('keeps a number on one line at its size', () => {
    expect(fitText('75', mono, square, 20, pt(12))).toEqual({
      size: 20,
      lines: ['75'],
      truncated: false,
    })
  })

  it('wraps a phrase at spaces, never inside a word', () => {
    const lines = wrap('the cell wall', mono, 16, 68)

    expect(lines).toEqual(['the', 'cell', 'wall'])
    expect(wrap('photosynthesis', mono, 16, 68)).toBeNull()
  })

  it('shrinks a square to fit before cutting anything off', () => {
    const fitted = fitText('red blood cell', mono, square, 20, pt(8))

    expect(fitted.truncated).toBe(false)
    expect(fitted.size).toBeLessThan(20)
    expect(fitted.size).toBeGreaterThanOrEqual(pt(8))
    expect(fitted.lines.join(' ')).toBe('red blood cell')
  })

  it('never goes below the minimum, and cuts off with "…" there instead', () => {
    const fitted = fitText('the powerhouse of the cell', mono, square, 20, pt(16))

    expect(fitted).toMatchObject({ size: pt(16), truncated: true })
    expect(fitted.lines.at(-1)).toMatch(/…$/)
    fitted.lines.forEach((line) => {
      expect(mono(line) * (pt(16) / 100)).toBeLessThanOrEqual(square.width)
    })
  })

  it('cuts inside a word only when one word is wider than the square on its own', () => {
    expect(truncate('photosynthesis', mono, pt(16), square)).toEqual(['phot…'])
  })

  it('sizes Max by the hardest face, which may not be the longest', () => {
    // "photosynthesis" (14 letters) is harder to fit than "red blood cell" (14 characters but three short words).
    const max = maxSizeForSet(['red blood cell', 'photosynthesis', '7'], mono, square, 0)

    expect(max).toBe(Math.floor((68 * 100) / (14 * 60)))
  })

  it('keeps Max at its floor (18 pt) or more, cutting off what still does not fit', () => {
    expect(maxSizeForSet(['photosynthesis'], mono, square, pt(18))).toBe(pt(18))
  })
})
