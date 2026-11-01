import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { normalizeCode, displayCode } from '../game/codes'
import { saveFile } from '../game/downloadSet'
import { Button } from '../game/ui'
import {
  howWon,
  minutesOf,
  roundPatterns,
  summaries,
  summaryCsv,
  timeOf,
  totals,
  wonWith,
} from './summary'

/**
 * The game summary (FR-40a), shown to the host when they end a game: the totals, the final results and
 * every round with its winners, to print (or save as PDF) or export as CSV. It lives only on this device.
 */
export function SummaryPage() {
  const code = normalizeCode(useParams().code ?? '')
  const summary = summaries.load(code)

  useEffect(() => {
    document.title = 'Game summary · SyncBingo'
    return () => {
      document.title = 'SyncBingo'
    }
  }, [])

  if (!summary) {
    return (
      <div className="m-auto flex flex-col items-center gap-3 p-6 text-center">
        <h1 className="font-display text-3xl font-extrabold tracking-tight">Game summary</h1>
        <p className="text-ink-muted">
          No summary is saved for this game in this browser tab. A summary is made when the host
          ends the game, and is kept only in that tab.
        </p>
        <Link to="/host/new" className="font-semibold text-accent-text underline">
          Host a new game
        </Link>
      </div>
    )
  }

  const total = totals(summary)
  const ended = new Date(summary.endedAt)

  return (
    <article className="mx-auto flex w-full max-w-2xl flex-col gap-5 pb-6 leading-relaxed">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-3xl font-extrabold tracking-tight">Game summary</h1>
        <p className="text-ink-muted">
          Game {displayCode(summary.code).toUpperCase()} · ended{' '}
          {ended.toLocaleDateString([], { dateStyle: 'long' })} at {timeOf(summary.endedAt)}
        </p>
      </header>

      <div className="flex flex-wrap gap-2 print:hidden">
        <Button onClick={printLight}>Print or save as PDF</Button>
        <Button
          look="secondary"
          onClick={() => {
            saveFile(
              `SyncBingo game ${summary.endedAt.slice(0, 10)}.csv`,
              new Blob([summaryCsv(summary)], { type: 'text/csv' }),
            )
          }}
        >
          Export as CSV
        </Button>
      </div>
      <p className="text-sm text-ink-muted print:hidden">
        This summary is only on this device, and the game is deleted from SyncBingo a few minutes
        after it ends. Print it or export it before you close this tab.
      </p>

      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {(
          [
            ['Players', total.players],
            ['Rounds', total.rounds],
            ['Balls called', total.balls],
            ['Wins', total.wins],
          ] as const
        ).map(([label, value]) => (
          <div key={label} className="rounded-2xl bg-surface p-3 text-center shadow-card">
            <dt className="text-sm text-ink-muted">{label}</dt>
            <dd className="font-display text-3xl font-extrabold">{value}</dd>
          </div>
        ))}
      </dl>

      <section aria-labelledby="final-results" className="flex flex-col gap-2">
        <h2 id="final-results" className="font-display text-2xl font-bold">
          Final results
        </h2>
        {summary.leaderboard.length === 0 ? (
          <p className="text-ink-muted">No wins this game.</p>
        ) : (
          <ol className="flex flex-col gap-1">
            {summary.leaderboard.map((entry, i) => (
              <li key={entry.participantId} className="flex justify-between gap-3">
                <span>
                  {i + 1}. {entry.name}
                </span>
                <span className="font-bold tabular-nums">
                  {entry.wins} win{entry.wins === 1 ? '' : 's'}
                </span>
              </li>
            ))}
          </ol>
        )}
      </section>

      <section aria-labelledby="rounds" className="flex flex-col gap-3">
        <h2 id="rounds" className="font-display text-2xl font-bold">
          Rounds
        </h2>
        {summary.rounds.length === 0 && <p className="text-ink-muted">No rounds were played.</p>}
        {summary.rounds.map((round) => {
          const minutes = minutesOf(round)
          return (
            <section
              key={round.number}
              aria-label={`Round ${String(round.number)}`}
              className="flex flex-col gap-1 rounded-2xl border-2 border-line-soft p-3 break-inside-avoid"
            >
              <h3 className="font-display text-lg font-bold">Round {round.number}</h3>
              <p className="text-sm text-ink-muted">
                {timeOf(round.startedAt)}
                {round.endedAt ? `–${timeOf(round.endedAt)}` : ''}
                {minutes !== undefined ? ` (${String(minutes)} min)` : ''} · {round.ballsCalled}{' '}
                ball{round.ballsCalled === 1 ? '' : 's'} called ·{' '}
                {roundPatterns(round, summary.patterns)} · {round.contentName ?? 'Numbers'}
              </p>
              {round.wins.length === 0 ? (
                <p>No winners.</p>
              ) : (
                <ul className="flex flex-col gap-0.5">
                  {round.wins.map((win, i) => (
                    <li key={i}>
                      <span className="font-semibold">{win.name}</span>:{' '}
                      {wonWith(win, summary.patterns)}
                      {win.wins > 1 ? ` (${String(win.wins)} wins)` : ''} ·{' '}
                      <span className="text-ink-muted">{howWon(win)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )
        })}
      </section>

      <p className="print:hidden">
        <Link to="/host/new" className="font-semibold text-accent-text underline">
          Host a new game
        </Link>
      </p>
    </article>
  )
}

/** Prints in the plain light colors, whatever this device shows (dark text on white paper), then restores them. */
function printLight() {
  const root = document.documentElement
  const dark = root.classList.contains('dark')
  const palette = root.dataset.palette
  root.classList.remove('dark')
  delete root.dataset.palette
  const restore = () => {
    root.classList.toggle('dark', dark)
    if (palette) root.dataset.palette = palette
    window.removeEventListener('afterprint', restore)
  }
  window.addEventListener('afterprint', restore)
  window.print()
}
