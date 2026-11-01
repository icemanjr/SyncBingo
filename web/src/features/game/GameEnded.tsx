import { Link } from 'react-router'
import type { Schemas } from '../../api/client'
import { summaries } from '../summary/summary'

/**
 * A game that is over: ended by the host, or gone from the server (an ended game is kept for 5 minutes,
 * an idle one for 4 hours, any game for 12 at most). Screens show this instead of an error. When the host
 * ended it, the final results (the leaderboard) show too; the host's screen links the game summary kept on
 * that device (FR-40a).
 */
export function GameEnded({
  hostLink = false,
  results,
  summaryCode,
}: {
  hostLink?: boolean
  /** The final leaderboard, when the game ended while this screen was open. */
  results?: Schemas['LeaderboardEntry'][] | null
  /** The game whose summary this device may hold (the host's). */
  summaryCode?: string
}) {
  const summaryLink =
    summaryCode !== undefined && summaries.load(summaryCode)
      ? `/host/${summaryCode}/summary`
      : undefined
  return (
    <div className="m-auto flex flex-col items-center gap-4 p-6 text-center">
      <h1 className="font-display text-3xl font-extrabold tracking-tight">This game has ended</h1>
      <p className="text-lg text-ink-muted">Thanks for playing!</p>
      {results && results.length > 0 && (
        <section
          aria-labelledby="final-results"
          className="w-full max-w-sm rounded-3xl bg-surface p-5 text-left shadow-card"
        >
          <h2 id="final-results" className="mb-2 font-display text-xl font-bold">
            Final results
          </h2>
          <ol className="flex flex-col gap-1">
            {results.map((entry, i) => (
              <li key={entry.participantId} className="flex justify-between gap-3">
                <span className="truncate">
                  {i + 1}. {entry.name}
                </span>
                <span className="font-bold tabular-nums">
                  {entry.wins} win{entry.wins === 1 ? '' : 's'}
                </span>
              </li>
            ))}
          </ol>
        </section>
      )}
      {summaryLink && (
        <Link to={summaryLink} className="font-semibold text-accent-text underline">
          See the game summary
        </Link>
      )}
      <Link
        to={hostLink ? '/host/new' : '/'}
        className="flex min-h-12 items-center rounded-2xl bg-accent px-6 font-bold text-accent-ink focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        {hostLink ? 'Host a new game' : 'Back to the start'}
      </Link>
    </div>
  )
}
