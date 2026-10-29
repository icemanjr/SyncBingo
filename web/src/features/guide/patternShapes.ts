/**
 * The standard patterns' shapes, for the How to play page, which shows them without a game to ask. A
 * game sends its own patterns as data (FR-48); these match the server's (StandardVariants, docs/03 §5).
 */
const shape = (...rows: string[]): number =>
  rows
    .join('')
    .split('')
    .reduce((mask, cell, i) => (cell === 'X' ? mask | (2 ** i) : mask), 0)

export const examplePatterns: { name: string; mask: number }[] = [
  { name: 'Normal Bingo: a row', mask: shape('.....', '.....', '.....', 'XXXXX', '.....') },
  { name: 'Normal Bingo: a column', mask: shape('.X...', '.X...', '.X...', '.X...', '.X...') },
  { name: 'Normal Bingo: a diagonal', mask: shape('X....', '.X...', '..X..', '...X.', '....X') },
  { name: 'Four Corners', mask: shape('X...X', '.....', '.....', '.....', 'X...X') },
  { name: 'Letter N', mask: shape('X...X', 'XX..X', 'X.X.X', 'X..XX', 'X...X') },
  { name: 'Blackout', mask: shape('XXXXX', 'XXXXX', 'XXXXX', 'XXXXX', 'XXXXX') },
]
