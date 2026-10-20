import { useRef, useState } from 'react'
import type { Schemas } from '../../api/client'
import {
  fitText,
  maxSizeForSet,
  useSquareMetrics,
  type Box,
  type Fitted,
  type Measure,
} from './fitText'
import { squareFace, type Content } from '../game/faces'
import { letterBackground } from '../game/letters'
import { Mark } from './Mark'
import { textSizes, type MarkStyle } from './markStyle'

/** Each square's fitted text, or nothing where text can't be measured (then plain CSS sizes apply). */
function fitSquares(
  faces: string[],
  setFaces: string[],
  size: (typeof textSizes)[number] | undefined,
  metrics: { box: Box; measure: Measure } | undefined,
): (Fitted | undefined)[] {
  if (!metrics || !size) return faces.map(() => undefined)
  const { box, measure } = metrics
  if (size.value === 'max') {
    const max = maxSizeForSet(setFaces, measure, box, size.min)
    return faces.map((face) => fitText(face, measure, box, max, max))
  }

  return faces.map((face) => fitText(face, measure, box, size.start, size.min))
}

/** How long a press must last to show a cut-off square's full text instead of marking it. */
const LongPressMs = 500

/**
 * A player's card. Each square is a toggle button (48 px or more): tap to mark, tap again to unmark (the
 * host's rules decide what's allowed). The mark sits behind the text, which stays bold on top with a halo
 * in the surface colour, so it reads at any opacity. "Highlight matching squares" outlines called, unmarked
 * squares with a dashed ring, so it doesn't rely on colour.
 *
 * Text is fitted to the squares (FR-51): it wraps at spaces only, shrinks a square if needed but never below
 * the chosen size's minimum, and is cut off with "…" past that. A cut-off square shows its full text on hover
 * and on a long press, which never marks it; screen readers always hear the full text.
 */
export function Card({
  card,
  columns,
  columnLabels,
  freeCell,
  called,
  highlightMatches,
  markStyle,
  setFaces,
  content,
  disabled,
  onToggle,
}: {
  card: Schemas['CardView']
  /** The custom set in use (ADR 0009): squares show its text, and the card uses tighter gaps for room. */
  content?: Content
  columns: number
  /** A label above each column (B-I-N-G-O), when the call board's groups match the card's columns. */
  columnLabels: string[] | null
  freeCell: number | null
  called: Set<number> | null
  highlightMatches: boolean
  markStyle: MarkStyle
  /** Every card face in the set; Max text size is fitted to the hardest of them, so all cards match. */
  setFaces: string[]
  disabled: boolean
  onToggle: (cell: number, marked: boolean) => void
}) {
  const firstSquare = useRef<HTMLButtonElement>(null)
  const metrics = useSquareMetrics(firstSquare)
  const size = textSizes.find((t) => t.value === markStyle.textSize) ?? textSizes[1]
  const [showingFull, setShowingFull] = useState<number>()
  const press = useRef<{ timer?: ReturnType<typeof setTimeout>; longPressed: boolean }>({
    longPressed: false,
  })
  const faces = card.cells.map((item, cell) =>
    cell === freeCell ? '★' : squareFace(content, item),
  )

  // Cheap enough to work out on each render: measurements are cached per text.
  const fitted = fitSquares(faces, setFaces, size, metrics)

  const endPress = () => {
    clearTimeout(press.current.timer)
  }

  return (
    <div
      role="grid"
      aria-label="Your card"
      className={`grid ${content ? 'gap-1' : 'gap-2'}`}
      style={{ gridTemplateColumns: `repeat(${String(columns)}, minmax(3rem, 1fr))` }}
    >
      {/* Seen, not read out: each square's name starts with its letter instead ("B 7"). */}
      {columnLabels?.map((label) => (
        <span
          key={label}
          aria-hidden="true"
          title={label}
          className={`flex h-9 min-w-0 items-center justify-center rounded-xl px-1 font-display font-extrabold text-white ${label.length > 2 ? 'text-sm' : 'text-xl'} ${letterBackground(label)}`}
        >
          <span className="truncate">{label}</span>
        </span>
      ))}
      {Array.from({ length: Math.ceil(card.cells.length / columns) }, (_, row) => (
        // A row for screen readers only ("contents" keeps the CSS grid's layout).
        <div key={row} role="row" className="contents">
          {card.cells
            .slice(row * columns, (row + 1) * columns)
            .map((number, i) => square(number, row * columns + i))}
        </div>
      ))}
    </div>
  )

  function square(number: number, cell: number) {
    const face = faces[cell] ?? ''
    const fit = fitted[cell]
    const marked = (card.marks & (2 ** cell)) !== 0
    const free = cell === freeCell
    const match = highlightMatches && !marked && called?.has(number) === true
    const letter = columnLabels?.[cell % columns]
    const label = `${free ? 'Free space' : `${letter ? `${letter} ` : ''}${face}`}${marked ? ', marked' : ''}${match ? ', called' : ''}`
    return (
      <button
        key={cell}
        ref={cell === 0 ? firstSquare : undefined}
        type="button"
        role="gridcell"
        aria-selected={marked}
        aria-label={label}
        title={fit?.truncated ? face : undefined}
        disabled={disabled}
        onPointerDown={() => {
          press.current.longPressed = false
          if (!fit?.truncated) return
          press.current.timer = setTimeout(() => {
            press.current.longPressed = true
            setShowingFull(cell)
          }, LongPressMs)
        }}
        onPointerUp={endPress}
        onPointerLeave={endPress}
        onPointerCancel={endPress}
        onContextMenu={(e) => {
          if (fit?.truncated) e.preventDefault() // a long press shows the text, not the phone's menu
        }}
        onClick={() => {
          if (press.current.longPressed) {
            press.current.longPressed = false
            return // the long press showed the full text; it doesn't mark
          }

          setShowingFull(undefined)
          onToggle(cell, !marked)
        }}
        className={`relative flex aspect-square min-h-12 flex-col items-center justify-center rounded-xl border-2 bg-surface font-display focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent ${
          match ? 'border-dashed border-accent' : 'border-line-soft'
        }`}
      >
        {marked && <Mark style={markStyle} />}
        <span
          aria-hidden="true"
          className={`relative z-10 flex flex-col items-center font-bold tabular-nums ${fit ? '' : (size?.className ?? 'text-xl')}`}
          style={{
            textShadow: '0 0 3px var(--surface), 0 0 3px var(--surface), 0 0 1px var(--surface)',
            fontSize: fit ? `${String(fit.size)}px` : undefined,
            lineHeight: 1.15,
          }}
        >
          {(fit?.lines ?? [face]).map((line, i) => (
            <span key={i} className="whitespace-nowrap">
              {line}
            </span>
          ))}
        </span>
        {showingFull === cell && (
          <span
            role="tooltip"
            className="absolute -top-2 left-1/2 z-30 -translate-x-1/2 -translate-y-full rounded-lg bg-ink px-2 py-1 text-sm whitespace-nowrap text-page"
          >
            {face}
          </span>
        )}
      </button>
    )
  }
}
