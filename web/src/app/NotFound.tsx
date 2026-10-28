import { Link } from 'react-router'

/** Any address the site doesn't have, e.g. a mistyped link: say so, and offer the way back. */
export function NotFound() {
  return (
    <div className="m-auto flex flex-col items-center gap-4 p-6 text-center">
      <h1 className="font-display text-3xl font-extrabold tracking-tight">Page not found</h1>
      <p className="text-lg text-ink-muted">
        That address isn’t part of SyncBingo. To join a game, scan its QR code or type its game
        code.
      </p>
      <Link
        to="/"
        className="flex min-h-12 items-center rounded-2xl bg-accent px-6 font-bold text-accent-ink focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        Back to the start
      </Link>
    </div>
  )
}
