import type { BallCall } from './gameEvents'
import type { Schemas } from '../../api/client'

/** Three columns per group (15 balls: 3 columns of 5), filled top to bottom. */
const SubColumns = 3
const rowsFor = (balls: number) => Math.ceil(balls / SubColumns)

/**
 * The call board (FR-27): every ball, grouped as the server says (B-I-N-G-O for standard Bingo), with
 * called balls marked by shape and weight as well as colour, and the last ball large on top. Each group's
 * balls run down its columns, then across (docs/06), so "was 27 called?" means one short run under "I".
 */
export function CallBoard({
  board,
  calls,
  big = false,
}: {
  board: Schemas['BoardGroup'][]
  calls: BallCall[]
  /** For the display view across a room (FR-28): larger balls and a much larger last ball. */
  big?: boolean
}) {
  const called = new Set(calls.map((call) => call.number))
  const last = calls.at(-1)
  return (
    <div className="flex flex-col items-center gap-4">
      <div aria-live="polite" className="text-center">
        <p className="text-sm text-ink-muted">Last Ball</p>
        <p
          className={`font-bold tabular-nums ${big ? 'text-8xl' : 'text-5xl'}`}
          data-testid="last-ball"
        >
          {last ? `${last.letter} ${String(last.number)}` : '–'}
        </p>
      </div>
      <div className="flex flex-wrap justify-center gap-3">
        {board.map((group) => (
          <div key={group.label} className="flex flex-col items-center gap-1">
            <span className={`font-bold ${big ? 'text-3xl' : ''}`}>{group.label}</span>
            <ol
              className="grid grid-flow-col gap-1"
              style={{ gridTemplateRows: `repeat(${String(rowsFor(group.balls.length))}, auto)` }}
              aria-label={`Balls ${group.label}`}
            >
              {group.balls.map((ball) => {
                const isCalled = called.has(ball)
                return (
                  <li
                    key={ball}
                    aria-label={`${group.label} ${String(ball)}${isCalled ? ', called' : ''}`}
                    className={`flex items-center justify-center rounded-full tabular-nums ${big ? 'h-12 w-12 text-xl' : 'h-8 w-8 text-sm'} ${
                      ball === last?.number
                        ? 'bg-amber-400 font-bold text-ink ring-4 ring-amber-600'
                        : isCalled
                          ? 'bg-accent font-bold text-accent-ink'
                          : 'text-ink-faint'
                    }`}
                  >
                    {ball}
                  </li>
                )
              })}
            </ol>
          </div>
        ))}
      </div>
    </div>
  )
}
