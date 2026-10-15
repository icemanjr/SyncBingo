import type { BallCall } from './gameEvents'

const sizes = {
  small: { ball: 'h-16 w-16', letter: 'text-xs', number: 'text-2xl' },
  /** Small on phones (the host's pinned calling bar), medium from tablets up. */
  responsive: {
    ball: 'h-[72px] w-[72px] sm:h-32 sm:w-32',
    letter: 'text-sm sm:text-xl',
    number: 'text-3xl sm:text-5xl',
  },
  medium: { ball: 'h-32 w-32', letter: 'text-xl', number: 'text-5xl' },
  large: { ball: 'h-56 w-56', letter: 'text-4xl', number: 'text-8xl' },
}

/**
 * The last ball called, as an amber ball with its letter over its number, announced to screen readers
 * as it changes. Before the first call it shows "–". Its text reads "B 5" (the test id finds it).
 */
export function LastBall({
  last,
  size = 'medium',
}: {
  last: BallCall | undefined
  size?: keyof typeof sizes
}) {
  const s = sizes[size]
  return (
    <p
      aria-live="polite"
      data-testid="last-ball"
      className={`flex shrink-0 flex-col items-center justify-center rounded-full bg-ball font-display leading-none font-extrabold text-ball-ink tabular-nums shadow-[inset_0_-6px_0_rgb(0_0_0/0.08)] ${s.ball} ${s.number}`}
    >
      {last ? (
        <>
          <span className={s.letter}>{last.letter}</span>{' '}
          <span className="tracking-tight">{last.number}</span>
        </>
      ) : (
        '–'
      )}
    </p>
  )
}
