import type { BallCall } from './gameEvents'
import type { Schemas } from '../../api/client'

/**
 * The call board (FR-27): every ball, grouped as the server says (B-I-N-G-O for standard Bingo), with
 * called balls marked by shape and weight as well as colour, and the last ball large on top.
 */
export function CallBoard({ board, calls }: { board: Schemas['BoardGroup'][]; calls: BallCall[] }) {
  const called = new Set(calls.map((call) => call.number))
  const last = calls.at(-1)
  return (
    <div className="flex flex-col items-center gap-4">
      <div aria-live="polite" className="text-center">
        <p className="text-sm text-ink-muted">Last ball</p>
        <p className="text-5xl font-bold tabular-nums" data-testid="last-ball">
          {last ? `${last.letter} ${String(last.number)}` : '–'}
        </p>
      </div>
      <div className="flex flex-wrap justify-center gap-3">
        {board.map((group) => (
          <div key={group.label} className="flex flex-col items-center gap-1">
            <span className="font-bold">{group.label}</span>
            <ol className="grid grid-cols-3 gap-1" aria-label={`Balls ${group.label}`}>
              {group.balls.map((ball) => {
                const isCalled = called.has(ball)
                return (
                  <li
                    key={ball}
                    aria-label={`${group.label} ${String(ball)}${isCalled ? ', called' : ''}`}
                    className={`flex h-8 w-8 items-center justify-center rounded-full text-sm tabular-nums ${
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
