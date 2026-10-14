import type { Schemas } from '../../api/client'
import { Mark } from './Mark'
import type { MarkStyle } from './markStyle'

/**
 * A player's card. Each square is a toggle button (48 px or more): tap to mark, tap again to unmark (the
 * host's rules decide what's allowed). The mark sits behind the number, which stays bold on top with a
 * halo in the surface colour, so it reads at any opacity. "Highlight matching squares" outlines called,
 * unmarked numbers with a dashed ring, so it doesn't rely on colour either.
 */
export function Card({
  card,
  columns,
  freeCell,
  called,
  highlightMatches,
  markStyle,
  disabled,
  onToggle,
}: {
  card: Schemas['CardView']
  columns: number
  freeCell: number | null
  called: Set<number> | null
  highlightMatches: boolean
  markStyle: MarkStyle
  disabled: boolean
  onToggle: (cell: number, marked: boolean) => void
}) {
  return (
    <div
      role="grid"
      aria-label="Your card"
      className="grid gap-1.5"
      style={{ gridTemplateColumns: `repeat(${String(columns)}, minmax(3rem, 1fr))` }}
    >
      {card.cells.map((number, cell) => {
        const marked = (card.marks & (2 ** cell)) !== 0
        const free = cell === freeCell
        const match = highlightMatches && !marked && called?.has(number) === true
        const label = `${free ? 'Free space' : String(number)}${marked ? ', marked' : ''}${match ? ', called' : ''}`
        return (
          <button
            key={cell}
            type="button"
            role="gridcell"
            aria-pressed={marked}
            aria-label={label}
            disabled={disabled}
            onClick={() => {
              onToggle(cell, !marked)
            }}
            className={`relative flex aspect-square min-h-12 items-center justify-center rounded-lg border-2 bg-surface focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent ${
              match ? 'border-dashed border-accent' : 'border-line-soft'
            }`}
          >
            {marked && <Mark style={markStyle} />}
            <span
              className="relative z-10 text-xl font-bold tabular-nums"
              style={{
                textShadow:
                  '0 0 3px var(--surface), 0 0 3px var(--surface), 0 0 1px var(--surface)',
              }}
            >
              {free ? '★' : number}
            </span>
          </button>
        )
      })}
    </div>
  )
}
