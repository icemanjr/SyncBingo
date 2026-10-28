import type { BallCall } from './gameEvents'

const sizes = {
  small: {
    ball: 'h-16 w-16',
    letter: 'text-xs',
    number: 'text-2xl',
    panel: 'max-w-[11rem] min-h-16 px-3 py-2',
  },
  /** Small on phones (the host's pinned calling bar), medium from tablets up. */
  responsive: {
    ball: 'h-[72px] w-[72px] sm:h-32 sm:w-32',
    letter: 'text-sm sm:text-xl',
    number: 'text-3xl sm:text-5xl',
    panel: 'max-w-[13rem] min-h-[72px] px-3 py-2 sm:max-w-sm sm:min-h-32 sm:px-5',
  },
  medium: {
    ball: 'h-32 w-32',
    letter: 'text-xl',
    number: 'text-5xl',
    panel: 'max-w-sm min-h-32 px-5 py-3',
  },
  large: {
    ball: 'h-56 w-56',
    letter: 'text-4xl',
    number: 'text-8xl',
    panel: 'max-w-2xl min-h-56 px-8 py-6',
  },
}

/**
 * A custom set's call text (ADR 0009) as large as its length allows: a word reads like a number, a
 * definition of up to 120 characters steps down so it still fits the panel.
 */
function textClass(text: string, size: keyof typeof sizes): string {
  const steps = {
    small: ['text-2xl', 'text-lg', 'text-sm', 'text-xs'],
    responsive: [
      'text-2xl sm:text-4xl',
      'text-lg sm:text-2xl',
      'text-sm sm:text-lg',
      'text-xs sm:text-base',
    ],
    medium: ['text-4xl', 'text-2xl', 'text-lg', 'text-base'],
    large: ['text-7xl', 'text-5xl', 'text-3xl', 'text-2xl'],
  }[size]
  const step = text.length <= 6 ? 0 : text.length <= 16 ? 1 : text.length <= 48 ? 2 : 3
  return steps[step] ?? ''
}

/**
 * The last ball called, as an amber ball with its letter over its number, announced to screen readers
 * as it changes. Before the first call it shows "–". Its text reads "B 5" (the test id finds it).
 *
 * With a custom set (ADR 0009) the call is text, from a word to a 120-character definition: the ball
 * becomes a rounded panel in the same color, its group's name (if any) over the text, which wraps.
 */
export function LastBall({
  last,
  size = 'medium',
}: {
  last: BallCall | undefined
  size?: keyof typeof sizes
}) {
  const s = sizes[size]
  if (last?.call != null) {
    return (
      <p
        aria-live="polite"
        data-testid="last-ball"
        className={`flex shrink-0 flex-col items-center justify-center gap-1 rounded-3xl bg-ball text-center font-display leading-tight font-extrabold text-balance text-ball-ink shadow-[inset_0_-6px_0_rgb(0_0_0/0.08)] ${s.panel}`}
      >
        {last.letter && <span className={s.letter}>{last.letter}</span>}{' '}
        <span className={`break-words ${textClass(last.call, size)}`}>{last.call}</span>
      </p>
    )
  }

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
