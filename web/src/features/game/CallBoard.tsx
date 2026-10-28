import type { BallCall } from './gameEvents'
import { ballFace, type Content } from './faces'
import { LastBall } from './LastBall'
import { letterBackground } from './letters'
import type { Schemas } from '../../api/client'

/** Three columns per group (15 balls: 3 columns of 5), filled top to bottom. */
const SubColumns = 3
const rowsFor = (balls: number) => Math.ceil(balls / SubColumns)

/**
 * The call board (FR-27): every ball, grouped as the server says (B-I-N-G-O for standard Bingo), with
 * called balls marked by shape and weight as well as color, and the last ball large on top. Each group's
 * balls run down its columns, then across (docs/06), so "was 27 called?" means one short run under "I".
 */
export function CallBoard({
  board,
  calls,
  content,
  big = false,
  showLastBall = true,
}: {
  board: Schemas['BoardGroup'][]
  calls: BallCall[]
  /** A custom set (ADR 0009): items show their ball text on wide tiles instead of numbers. */
  content?: Content
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
      {content ? (
        <TextBoard board={board} called={called} last={last?.number} content={content} big={big} />
      ) : (
        /* One row of groups, spread across the width; balls shrink on phones so five groups fit. */
        <div className={`flex w-full justify-between ${big ? 'gap-3' : 'gap-1 sm:gap-2'}`}>
          {board.map((group) => (
            <div
              key={group.label}
              className={`flex flex-col items-center rounded-2xl bg-group ${big ? 'gap-3 p-1.5 pb-3' : 'gap-1.5 p-0.5 pb-1.5 sm:gap-2 sm:p-1.5 sm:pb-2'}`}
            >
              <span
                className={`flex items-center justify-center rounded-full font-display font-extrabold text-white ${letterBackground(group.label)} ${big ? 'h-12 w-12 text-2xl min-[1800px]:h-16 min-[1800px]:w-16 min-[1800px]:text-4xl' : 'h-7 w-7 text-base sm:h-9 sm:w-9 sm:text-lg'}`}
              >
                {group.label}
              </span>
              <ol
                className={`grid grid-flow-col ${big ? 'gap-x-1 gap-y-1.5 min-[1800px]:gap-x-2' : 'gap-0.5 sm:gap-1'}`}
                style={{ gridTemplateRows: `repeat(${String(rowsFor(group.balls.length))}, auto)` }}
                aria-label={`Balls ${group.label}`}
              >
                {group.balls.map((ball) => {
                  const isCalled = called.has(ball)
                  return (
                    <li
                      key={ball}
                      aria-label={`${group.label} ${String(ball)}${isCalled ? ', called' : ''}`}
                      className={`flex items-center justify-center rounded-full tabular-nums ${big ? 'h-11 w-11 font-display text-lg min-[1800px]:h-16 min-[1800px]:w-16 min-[1800px]:text-2xl' : 'h-[18px] w-[18px] text-[10px] sm:h-[30px] sm:w-[30px] sm:text-[13px]'} ${
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
      )}
    </div>
  )
}

/**
 * The board for a custom set (ADR 0009, docs/06): each group's items as wide tiles showing their ball
 * text (FR-29: the ball side, the default), in the set's order, cut off with "…" (full text on hover and
 * for screen readers). Groups are stacked, each under its name; a set dealt free-form is one block.
 */
function TextBoard({
  board,
  called,
  last,
  content,
  big,
}: {
  board: Schemas['BoardGroup'][]
  called: Set<number>
  last: number | undefined
  content: Content
  big: boolean
}) {
  return (
    <div className="flex w-full flex-col gap-3">
      {board.map((group, index) => (
        <section
          key={group.label || index}
          aria-label={group.label ? `Balls ${group.label}` : 'Balls'}
          className={`flex flex-col gap-2 rounded-2xl bg-group ${big ? 'p-3' : 'p-1.5 sm:p-2'}`}
        >
          {group.label && (
            <h3
              className={`self-start rounded-full px-3 font-display font-extrabold text-white ${letterBackground(group.label)} ${big ? 'py-1 text-2xl' : 'py-0.5 text-base'}`}
            >
              {group.label}
            </h3>
          )}
          <ol
            className={`grid gap-1 ${big ? 'grid-cols-[repeat(auto-fill,minmax(11rem,1fr))] gap-1.5' : 'grid-cols-[repeat(auto-fill,minmax(6.5rem,1fr))]'}`}
          >
            {group.balls.map((item) => {
              const face = ballFace(content, item)
              const isCalled = called.has(item)
              return (
                <li
                  key={item}
                  title={face}
                  aria-label={`${face}${isCalled ? ', called' : ''}`}
                  className={`truncate rounded-lg px-2 text-center ${big ? 'py-1.5 font-display text-lg' : 'py-0.5 text-xs sm:text-sm'} ${
                    item === last
                      ? 'bg-ball font-extrabold text-ball-ink ring-2 ring-[#f0a800]'
                      : isCalled
                        ? `${letterBackground(group.label)} font-extrabold text-white`
                        : 'font-medium text-ink-faint'
                  }`}
                >
                  {face}
                </li>
              )
            })}
          </ol>
        </section>
      ))}
    </div>
  )
}
