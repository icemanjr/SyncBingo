import type { Schemas } from '../../api/client'
import type { BallCall } from './gameEvents'

/** The custom set in use (ADR 0009), or none for numbers. */
export type Content = Schemas['ContentView'] | null | undefined

/**
 * What a ball shows on the board (ADR 0009): with a custom set, item n's ball text (`content.board[n − 1]`,
 * e.g. "2 + 5"); without one, the number. The snapshot never pairs balls with squares (that would be the
 * answer key): cards bring their own square text, and calls carry both of their texts.
 */
export const ballFace = (content: Content, item: number): string =>
  content?.board[item - 1] ?? String(item)

/** What a call says: its own text with a set (calls carry it), else the number. */
export const callFace = (call: BallCall): string => call.call ?? String(call.number)

/**
 * A card square's text: the card's own faces with a set (its cells are then placeholders, not items),
 * else the number.
 */
export const cardFace = (card: Schemas['CardView'], cell: number): string =>
  card.faces?.[cell] ?? String(card.cells[cell])

/**
 * Every square face in the game, for **Max** text size (FR-51): one size fitted to the hardest face, so
 * every card looks the same. With a set, its squares (alphabetical, so they pair with nothing); else the
 * board's numbers.
 */
export const allSquareFaces = (content: Content, board: { balls: number[] }[]): string[] =>
  content ? content.squares : board.flatMap((group) => group.balls).map(String)

/**
 * Whether a square has been called. With a set, a player's card holds no item numbers, so its text is
 * matched against the called balls' squares (each square's text is unique in a set); else by number.
 */
export function calledTest(
  card: Schemas['CardView'],
  calls: BallCall[] | null,
): ((cell: number) => boolean) | null {
  if (!calls) return null
  if (card.faces) {
    const squares = new Set(calls.map((call) => call.square ?? ''))
    return (cell) => squares.has(card.faces?.[cell] ?? '')
  }

  const numbers = new Set(calls.map((call) => call.number))
  return (cell) => numbers.has(card.cells[cell] ?? -1)
}
