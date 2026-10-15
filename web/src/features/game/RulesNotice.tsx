import type { PatternData } from './patterns'
import type { RulesNotice as Notice } from './gameEvents'
import { describeChange } from './rules'

/**
 * "Rules updated" (FR-47), on every screen, so players who can't hear the announcement still know. It
 * closes with X or when the next ball is called, and never covers the Bingo button (it sits in the flow).
 */
export function RulesNotice({
  notice,
  patterns,
  onClose,
}: {
  notice: Notice
  /** The game's patterns and presets, to name a new selection. */
  patterns: PatternData
  onClose: () => void
}) {
  return (
    <section
      aria-live="polite"
      aria-label="Rules updated"
      className="rounded-2xl border-2 border-accent bg-accent-soft p-4"
    >
      <div className="flex items-start justify-between gap-3">
        <h2 className="text-lg font-bold">Rules updated</h2>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="flex min-h-12 min-w-12 items-center justify-center rounded-xl text-xl focus-visible:outline-4 focus-visible:outline-accent"
        >
          ✕
        </button>
      </div>
      <ul className="list-disc pl-5">
        {notice.changes.map((change) => (
          <li key={change.rule}>{describeChange(change, patterns)}</li>
        ))}
      </ul>
    </section>
  )
}
