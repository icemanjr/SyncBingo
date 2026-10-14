import { Link } from 'react-router'

/** The front door (FR-1): host or join; the Full portfolio build also shows a greyed-out tile for what may come. */
export function LandingPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-8 text-center">
      <div>
        <h1 className="text-4xl font-bold tracking-tight">SyncBingo</h1>
        <p className="mt-2 text-lg text-ink-muted">Bingo for a room full of phones.</p>
      </div>
      <div className="flex w-full max-w-sm flex-col gap-4">
        <Link
          to="/host/new"
          className="flex min-h-12 items-center justify-center rounded-xl bg-accent px-6 text-lg font-semibold text-accent-ink hover:bg-accent-strong focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Host a game
        </Link>
        <Link
          to="/join"
          className="flex min-h-12 items-center justify-center rounded-xl border-2 border-accent px-6 text-lg font-semibold text-accent-text hover:bg-accent-soft focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Join a game
        </Link>
        {/* Build-time switch: off on the live Lean site, on for the Full portfolio build (ADR 0011). Written
            inline so the Lean build drops the tile entirely instead of shipping it hidden. */}
        {import.meta.env.VITE_SERIOUS_BINGO_TEASER === 'true' && (
          <div
            aria-disabled="true"
            className="flex min-h-12 cursor-not-allowed flex-col items-center justify-center rounded-xl border-2 border-dashed border-line px-6 py-2 text-ink-muted"
          >
            <span className="font-semibold">Serious Bingo</span>
            <span className="text-sm">Coming soon</span>
          </div>
        )}
      </div>
    </div>
  )
}
