import { describe, expect, it } from 'vitest'
import { fitFontSize } from './fitText'

describe('fitFontSize (Max text size)', () => {
  it('fills the square across when the text is the wide part', () => {
    // "75" is 112 px wide at 100 px; an 80 px square leaves 68 px across.
    expect(fitFontSize(112, 80, 80)).toBe(60)
  })

  it('is capped by the height for short text', () => {
    // A single character at 50 px wide per 100 px would be 136 px tall; the square's height limits it.
    expect(fitFontSize(50, 80, 80)).toBe(60)
  })

  it('shrinks for long words, so they never wrap or get cut off', () => {
    // "photosynthesis" at 100 px is about 760 px wide.
    expect(fitFontSize(760, 80, 80)).toBe(8)
  })

  it('gives nothing when it cannot measure, so the caller falls back', () => {
    expect(fitFontSize(0, 80, 80)).toBe(0)
  })
})
