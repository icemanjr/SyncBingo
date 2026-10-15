import type { Schemas } from '../../api/client'
import { PatternGrid } from './PatternGrid'
import { selectedPatterns } from './patterns'

/**
 * One small grid per selected pattern (FR-21), each showing one way to win it, e.g. a row, a column, a
 * diagonal and the four corners. Shown with the round's name on the display and during the countdown.
 */
export function SelectionGrids({ game }: { game: Schemas['GameView'] }) {
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      {selectedPatterns(game.winCondition, game).map((pattern) => (
        <PatternGrid
          key={pattern.id}
          mask={pattern.masks[0] ?? 0}
          columns={game.card.columns}
          rows={game.card.rows}
          freeCell={game.card.freeCell}
          earned={game.settings.freeSpace === 'Earned'}
          label={`${pattern.name} pattern`}
        />
      ))}
    </span>
  )
}
