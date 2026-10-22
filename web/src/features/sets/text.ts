/**
 * The text rules for a custom set (ADR 0009), as the server applies them (`ContentText`), so the editor
 * can say what's wrong while the host types. The server checks again; its answer is the one that counts.
 */

/** Trimmed, single-spaced (tabs and line breaks become spaces) and Unicode-normalised (NFKC). */
export const tidy = (value: string | null | undefined): string =>
  (value ?? '').normalize('NFKC').trim().replace(/\s+/g, ' ')

const graphemes = new Intl.Segmenter(undefined, { granularity: 'grapheme' })

/** Characters as a reader counts them: "é" and "👍🏽" are one each. */
export const length = (text: string): number => [...graphemes.segment(text)].length

/** Fits `max` characters, and at most three UTF-16 units each, so stacked accents can't sneak past. */
export const fits = (text: string, max: number): boolean =>
  text.length <= max * 3 && length(text) <= max

/** Printable: no control, formatting (bar the zero-width joiner in emoji), private-use or unassigned characters. */
export const isPrintable = (text: string): boolean =>
  !/[\p{Cc}\p{Co}\p{Cn}\p{Cs}\u2028\u2029]/u.test(text) &&
  !/\p{Cf}/u.test(text.replace(/\u200d/g, ''))

/** The form compared for uniqueness: "Cat" and "cat" are the same square. */
export const key = (text: string): string => text.toLowerCase()

/** A group's label when the host gives none: its first letter, upper case ("Biology" → "B"). */
export const defaultLabel = (group: string): string => {
  const first = graphemes.segment(group)[Symbol.iterator]().next()
  return first.done ? '' : first.value.segment.toUpperCase()
}

/** The limits the server checks (ADR 0009). */
export const limits = {
  minEntries: 24,
  maxEntries: 500,
  ball: 120,
  square: 15,
  groups: 5,
  groupName: 20,
  label: 5,
  name: 60,
  fileBytes: 64 * 1024,
} as const
