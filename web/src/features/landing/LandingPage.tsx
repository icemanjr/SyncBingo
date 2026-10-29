import { Link } from 'react-router'
import { Glossary } from '../guide/Glossary'

const steps = [
  ['Host a game', 'Pick a password, display the game code on this device or a TV.'],
  ['Everyone scans to join', 'Each player gets their own card on their own device.'],
  ['Call, mark, shout Bingo', 'The app checks every claim against the balls actually called.'],
] as const

/** A decorative bingo ball for the hero (hidden from screen readers). */
function Ball({ text, className }: { text: string; className: string }) {
  return (
    <span
      aria-hidden="true"
      className={`absolute flex items-center justify-center rounded-full font-display font-extrabold ${className}`}
    >
      {text}
    </span>
  )
}

/**
 * The front door (FR-1, the design reference): what SyncBingo is, **Join a game** (most visitors) and
 * **Host a game**, and how it works in three steps. The Full portfolio build also shows a grayed-out tile
 * for what may come.
 */
export function LandingPage() {
  return (
    <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center gap-6">
      <section className="relative overflow-hidden rounded-[28px] bg-[#1c1633] px-6 pt-28 pb-6 text-white dark:bg-[#2a2245]">
        <Ball text="B 7" className="top-8 right-36 h-16 w-16 bg-letter-b text-lg text-white" />
        <Ball text="N 37" className="-top-3 right-12 h-24 w-24 bg-ball text-2xl text-ball-ink" />
        <Ball text="G 52" className="top-14 -right-4 h-16 w-16 bg-letter-g text-lg text-white" />
        <h1 className="font-display text-4xl leading-none font-extrabold tracking-tight">
          Bingo for a room full of phones.
        </h1>
        <p className="mt-3 mb-6 text-base leading-relaxed text-[#d9d3ea]">
          One person hosts. Everyone else scans a code and plays on their own device. No app, no
          sign-up.
        </p>
        <div className="flex flex-col gap-3">
          <Link
            to="/join"
            className="flex min-h-14 items-center justify-center rounded-2xl bg-ball font-display text-lg font-extrabold text-ball-ink focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-ball"
          >
            Join a game
          </Link>
          <Link
            to="/host/new"
            className="flex min-h-14 items-center justify-center rounded-2xl border-2 border-white/50 text-lg font-bold text-white hover:bg-white/10 focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-white"
          >
            Host a game
          </Link>
        </div>
      </section>

      <section aria-label="How it works" className="flex flex-col gap-3 px-1">
        {steps.map(([title, text], i) => (
          <div key={title} className="flex items-start gap-3.5">
            <span
              aria-hidden="true"
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent-soft font-extrabold text-accent-text"
            >
              {i + 1}
            </span>
            <p className="leading-snug">
              <span className="block font-bold">{title}</span>
              <span className="text-sm text-ink-muted">{text}</span>
            </p>
          </div>
        ))}
      </section>

      <section aria-labelledby="about" className="flex flex-col gap-3 px-1 leading-relaxed">
        <h2 id="about" className="font-display text-2xl font-bold">
          What is SyncBingo?
        </h2>
        <p>
          SyncBingo is Bingo for any group, played on the devices everyone already has. Players can
          be in the same room or anywhere else: all they need is the game code and password from the
          host, in person, on a call or in a message. The host runs the game from any device, such
          as a laptop, tablet or phone. Everyone else scans a QR code or types the game code, then
          the game’s password and a name, and gets a card of their own. Balls are drawn on screen
          and reach every device at once, and the host can put the call board on a TV, or share
          their screen on a video call, so everyone can follow.
        </p>
        <p>
          It suits a family party with small children, a classroom, an office get-together or a
          group of adults playing for bragging rights. The host decides how forgiving it is: marking
          only called squares, auto-fill for missed balls and win celebrations for the youngest, or
          no help at all for players who want to concentrate. Teachers can swap the numbers for
          their own words, sums or clues.
        </p>
        <p>
          There’s nothing to install and no accounts. A game keeps only the names people type and
          their cards, and everything is deleted a few minutes after the host ends it.
        </p>
        <p className="flex flex-wrap gap-x-4 gap-y-2 font-semibold">
          <Link to="/how-to-play" className="text-accent-text underline">
            How to play
          </Link>
          <Link to="/faq" className="text-accent-text underline">
            Questions and answers
          </Link>
        </p>
      </section>

      <section aria-labelledby="words" className="flex flex-col gap-3 px-1 leading-relaxed">
        <h2 id="words" className="font-display text-2xl font-bold">
          Words SyncBingo uses
        </h2>
        <p className="text-ink-muted">
          Some of these mean something different in other games, so here is how SyncBingo uses them.
        </p>
        <Glossary />
      </section>

      {/* Build-time switch: off on the live Lean site, on for the Full portfolio build (ADR 0011). Written
          inline so the Lean build drops the tile entirely instead of shipping it hidden. */}
      {import.meta.env.VITE_SERIOUS_BINGO_TEASER === 'true' && (
        <div
          aria-disabled="true"
          className="flex min-h-12 cursor-not-allowed flex-col items-center justify-center rounded-2xl border-2 border-dashed border-line px-6 py-2 text-ink-muted"
        >
          <span className="font-semibold">Serious Bingo</span>
          <span className="text-sm">Coming soon</span>
        </div>
      )}
    </div>
  )
}
