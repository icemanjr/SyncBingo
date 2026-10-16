import { Link } from 'react-router'

/**
 * A game that is over: ended by the host, or gone from the server (an ended game is kept for 5 minutes,
 * an idle one for 4 hours, any game for 12 at most). Screens show this instead of an error.
 */
export function GameEnded({ hostLink = false }: { hostLink?: boolean }) {
  return (
    <div className="m-auto flex flex-col items-center gap-4 p-6 text-center">
      <h1 className="font-display text-3xl font-extrabold tracking-tight">This game has ended</h1>
      <p className="text-lg text-ink-muted">Thanks for playing!</p>
      <Link
        to={hostLink ? '/host/new' : '/'}
        className="flex min-h-12 items-center rounded-2xl bg-accent px-6 font-bold text-accent-ink focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        {hostLink ? 'Host a new game' : 'Back to the start'}
      </Link>
    </div>
  )
}
