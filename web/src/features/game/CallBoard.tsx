import type { BallCall } from './gameEvents'
import { LastBall } from './LastBall'
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
  showLastBall = true,
}: {
  board: Schemas['BoardGroup'][]
  calls: BallCall[]
  /** For the display view across a room (FR-28): larger balls and a much larger last ball. */
  big?: boolean
  /** Off where the screen shows the last ball elsewhere (the host's calling card). */
  showLastBall?: boolean
}) {
  const called = new Set(calls.map((call) => call.number))
  const last = calls.at(-1)
  return (
    <div className="flex flex-col items-center gap-4">
      {showLastBall && (
        <div className="flex flex-col items-center gap-1.5">
          <p className="text-xs font-bold tracking-widest text-ink-muted uppercase">Last Ball</p>
          <LastBall last={last} size={big ? 'large' : 'medium'} />
        </div>
      )}
      {/* One row of groups, spread across the width; balls shrink on phones so five groups fit. */}
      <div className={`flex w-full justify-between ${big ? 'gap-3' : 'gap-1 sm:gap-2'}`}>
        {board.map((group) => (
          <div
            key={group.label}
            className={`flex flex-col items-center rounded-2xl bg-group ${big ? 'gap-3 p-2 pb-3' : 'gap-1.5 p-0.5 pb-1.5 sm:gap-2 sm:p-1.5 sm:pb-2'}`}
          >
            <span
              className={`flex items-center justify-center rounded-full font-display font-extrabold text-white ${letterBackground(group.label)} ${big ? 'h-14 w-14 text-3xl' : 'h-7 w-7 text-base sm:h-9 sm:w-9 sm:text-lg'}`}
            >
              {group.label}
            </span>
            <ol
              className={`grid grid-flow-col ${big ? 'gap-1.5' : 'gap-0.5 sm:gap-1'}`}
              style={{ gridTemplateRows: `repeat(${String(rowsFor(group.balls.length))}, auto)` }}
              aria-label={`Balls ${group.label}`}
            >
              {group.balls.map((ball) => {
                const isCalled = called.has(ball)
                return (
                  <li
                    key={ball}
                    aria-label={`${group.label} ${String(ball)}${isCalled ? ', called' : ''}`}
                    className={`flex items-center justify-center rounded-full tabular-nums ${big ? 'h-12 w-12 text-xl' : 'h-[18px] w-[18px] text-[10px] sm:h-[30px] sm:w-[30px] sm:text-[13px]'} ${
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
