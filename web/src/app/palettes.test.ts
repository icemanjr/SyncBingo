import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { paletteColours, paletteCss, palettes } from './palettes'

/** Classic's colours, read from index.css itself, so the test checks what the page really uses. */
const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')
function tokensOf(selector: string): Record<string, string> {
  const body = new RegExp(`\\n${selector} \\{([^}]*)\\}`).exec(css)?.[1] ?? ''
  const tokens: Record<string, string> = {}
  for (const [, name, value] of body.matchAll(/--([a-z-]+):\s*(#[0-9a-f]{6});/g))
    if (name && value) tokens[name] = value
  return tokens
}
const light = tokensOf(':root')
const dark = { ...light, ...tokensOf('\\.dark') }

/** WCAG 2.1 relative luminance and contrast ratio. */
function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  }) as [number, number, number]
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}
const contrast = (a: string, b: string) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number]
  return (hi + 0.05) / (lo + 0.05)
}

/** Text on its backgrounds (4.5:1, WCAG AA) and the accent as a border or focus ring (3:1). */
const textPairs: [string, string][] = [
  ['ink', 'page'],
  ['ink', 'surface'],
  ['ink', 'group'],
  ['ink-muted', 'page'],
  ['ink-muted', 'surface'],
  ['ink-muted', 'group'],
  ['accent-text', 'page'],
  ['accent-text', 'surface'],
  ['accent-text', 'accent-soft'],
  ['accent-ink', 'accent'],
  ['ball-ink', 'ball'],
]
const borderPairs: [string, string][] = [
  ['accent', 'page'],
  ['accent', 'surface'],
]

describe('colour themes (FR-50a)', () => {
  const all: { name: string; light: Record<string, string>; dark: Record<string, string> }[] = [
    { name: 'Classic', light, dark },
    ...Object.entries(paletteColours).map(([name, colours]) => ({
      name,
      light: { ...light, ...colours.light },
      dark: { ...dark, ...colours.dark },
    })),
  ]

  for (const palette of all) {
    for (const mode of ['light', 'dark'] as const) {
      it(`${palette.name}, ${mode}: text meets 4.5:1 and the accent 3:1`, () => {
        const colours = palette[mode]
        for (const [fg, bg] of textPairs) {
          const ratio = contrast(must(colours[fg]), must(colours[bg]))
          expect(ratio, `${fg} on ${bg}`).toBeGreaterThanOrEqual(4.5)
        }
        for (const [fg, bg] of borderPairs) {
          const ratio = contrast(must(colours[fg]), must(colours[bg]))
          expect(ratio, `${fg} on ${bg}`).toBeGreaterThanOrEqual(3)
        }
      })
    }
  }

  it('lists every palette the server knows, and writes a light and a dark rule for each', () => {
    expect(palettes.map((p) => p.value)).toEqual([
      'Classic',
      'Ocean',
      'Forest',
      'Halloween',
      'Winter',
    ])
    for (const name of Object.keys(paletteColours)) {
      expect(paletteCss()).toContain(`:root[data-palette="${name}"]{`)
      expect(paletteCss()).toContain(`:root.dark[data-palette="${name}"]{`)
    }
  })
})

function must(value: string | undefined): string {
  if (value === undefined) throw new Error('missing colour')
  return value
}
