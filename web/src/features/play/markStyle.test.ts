import { afterEach, describe, expect, it } from 'vitest'
import { defaultMarkStyle, loadMarkStyle } from './markStyle'

describe('mark style (FR-51)', () => {
  afterEach(() => {
    localStorage.clear()
  })

  it('starts as a blue circle at 45%', () => {
    expect(loadMarkStyle()).toEqual({
      shape: 'circle',
      colour: '#2563eb',
      opacity: 0.45,
      textSize: 'small',
    })
  })

  it('keeps opacity between 20% and 80%, and ignores anything it does not know', () => {
    localStorage.setItem(
      'syncbingo:mark-style',
      JSON.stringify({ shape: 'skull', colour: 'url(evil)', opacity: 3, textSize: 'huge' }),
    )

    expect(loadMarkStyle()).toEqual({ ...defaultMarkStyle, opacity: 0.8 })
  })
})
