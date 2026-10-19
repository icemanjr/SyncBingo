import type { Schemas } from '../../api/client'
import type { BallCall } from './gameEvents'

/** The custom set in use (ADR 0009), or none for numbers. */
export type Content = Schemas['ContentView'] | null | undefined

/**
 * What an item shows (ADR 0009). Cards, calls and the board hold item numbers; with a custom set,
 * `content.items[n − 1]` gives item n's two faces: the **ball** (what is called, e.g. "2 + 5") and the
 * **square** (what players mark, e.g. "7"). Without one, both faces are the number.
 */
export const ballFace = (content: Content, item: number): string =>
  content?.items[item - 1]?.ball ?? String(item)

export const squareFace = (content: Content, item: number): string =>
  content?.items[item - 1]?.square ?? String(item)

/** What a call says: its own text with a set (calls carry it), else the number. */
export const callFace = (call: BallCall): string => call.call ?? String(call.number)

/**
 * Every card face in the game, for **Max** text size (FR-51): one size fitted to the hardest face, so
 * every card looks the same. The board lists every item.
 */
export const allSquareFaces = (content: Content, board: { balls: number[] }[]): string[] =>
  board.flatMap((group) => group.balls).map((item) => squareFace(content, item))
