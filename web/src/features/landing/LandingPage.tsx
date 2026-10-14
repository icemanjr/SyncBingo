import { Link } from 'react-router'

/** The front door (FR-1): host or join, plus a greyed-out tile for what's coming. */
export function LandingPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-8 text-center">
      <div>
        <h1 className="text-4xl font-bold tracking-tight">SyncBingo</h1>
        <p className="mt-2 text-lg text-slate-600">Bingo for a room full of phones.</p>
      </div>
      <div className="flex w-full max-w-sm flex-col gap-4">
        <Link
          to="/host/new"
          className="flex min-h-12 items-center justify-center rounded-xl bg-indigo-600 px-6 text-lg font-semibold text-white hover:bg-indigo-700 focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
        >
          Host a game
        </Link>
        <Link
          to="/join"
          className="flex min-h-12 items-center justify-center rounded-xl border-2 border-indigo-600 px-6 text-lg font-semibold text-indigo-700 hover:bg-indigo-50 focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
        >
          Join a game
        </Link>
        <div
          aria-disabled="true"
          className="flex min-h-12 cursor-not-allowed flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-300 px-6 py-2 text-slate-500"
        >
          <span className="font-semibold">Serious Bingo</span>
          <span className="text-sm">Coming soon</span>
        </div>
      </div>
    </div>
  )
}
