/**
 * A pattern drawn on a small card grid (FR-48), from the mask the server sends: bit *i* is cell *i*.
 * Filled and outlined, not colour alone; the free cell shows ★, or "earn it first" when it is earned.
 */
export function PatternGrid({
  mask,
  columns,
  rows,
  freeCell,
  earned,
  label,
}: {
  mask: number
  columns: number
  rows: number
  freeCell: number | null
  earned: boolean
  label: string
}) {
  return (
    <div
      role="img"
      aria-label={label}
      className="inline-grid gap-0.5"
      style={{ gridTemplateColumns: `repeat(${String(columns)}, 1rem)` }}
    >
      {Array.from({ length: rows * columns }, (_, cell) => {
        const inPattern = (mask & (2 ** cell)) !== 0
        return (
          <span
            key={cell}
            className={`flex h-4 w-4 items-center justify-center rounded-sm border text-[0.6rem] leading-none ${
              inPattern ? 'border-ink bg-ink text-page' : 'border-line'
            }`}
          >
            {cell === freeCell ? (earned ? '?' : '★') : ''}
          </span>
        )
      })}
    </div>
  )
}
