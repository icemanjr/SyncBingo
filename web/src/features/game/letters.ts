/**
 * Each B-I-N-G-O letter's colour, as on real bingo balls, for group headers and called balls. The letter
 * is always written too, so colour is never the only cue (WCAG); other labels use the accent.
 */
const letterColours: Record<string, string> = {
  B: 'bg-letter-b',
  I: 'bg-letter-i',
  N: 'bg-letter-n',
  G: 'bg-letter-g',
  O: 'bg-letter-o',
}

/** The background class for a group label; white text reads on every one (AA). */
export const letterBackground = (label: string): string => letterColours[label] ?? 'bg-accent'
