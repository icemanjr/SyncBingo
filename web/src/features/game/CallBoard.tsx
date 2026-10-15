import type { BallCall } from './gameEvents'
import { letterBackground } from './letters'
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
      <div aria-live="polite" className="flex flex-col items-center gap-1.5">
        <p className="text-xs font-bold tracking-widest text-ink-muted uppercase">Last Ball</p>
        <p
          className={`flex flex-col items-center justify-center rounded-full bg-ball font-display leading-none font-extrabold text-ball-ink tabular-nums shadow-[inset_0_-6px_0_rgb(0_0_0/0.08)] ${big ? 'h-56 w-56 text-8xl' : 'h-32 w-32 text-5xl'}`}
          data-testid="last-ball"
        >
          {last ? (
            <>
              <span className={big ? 'text-4xl' : 'text-xl'}>{last.letter}</span>{' '}
              <span className="tracking-tight">{last.number}</span>
            </>
          ) : (
            '–'
          )}
        </p>
      </div>
      <div className="flex flex-wrap justify-center gap-3">
        {board.map((group) => (
          <div
            key={group.label}
            className="flex flex-col items-center gap-2 rounded-2xl bg-group p-1.5 pb-2"
          >
            <span
              className={`flex items-center justify-center rounded-full font-display font-extrabold text-white ${letterBackground(group.label)} ${big ? 'h-14 w-14 text-3xl' : 'h-9 w-9 text-lg'}`}
            >
              {group.label}
            </span>
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
                        ? 'bg-ball font-extrabold text-ball-ink ring-2 ring-[#f0a800]'
                        : isCalled
                          ? `${letterBackground(group.label)} font-extrabold text-white`
                          : 'font-medium text-ink-faint'
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
