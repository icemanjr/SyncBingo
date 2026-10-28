import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { PatternGrid } from '../game/PatternGrid'
import { selectedPatterns, selectionName } from '../game/patterns'
import { describeChange } from '../game/rules'
import { Button, Panel } from '../game/ui'

type Game = Schemas['GameView']

/**
 * The masks to draw for one pattern, with what each shows. A line pattern shows one of its lines ("Any of
 * the 5 rows"); a shape shows up to three of its ways to win.
 */
function examples(pattern: Schemas['PatternView']): { mask: number; label: string }[] {
  if (pattern.isLine) {
    const count = pattern.masks.length
    return [
      {
        mask: pattern.masks[0] ?? 0,
        label:
          count === 1 ? pattern.name : `Any of the ${String(count)} ${pattern.name.toLowerCase()}`,
      },
    ]
  }

  return pattern.masks.slice(0, 3).map((mask, i) => ({
    mask,
    label: `${pattern.name}${pattern.masks.length > 1 ? `, way ${String(i + 1)}` : ''}`,
  }))
}

/** The rules that apply to players right now, in plain words (FR-48). */
function currentRules(game: Game, autoFillOn: boolean): string[] {
  const s = game.settings
  const say = (rule: string, value: string | boolean) =>
    describeChange({ rule, value: String(value) }, game)
  return [
    say('freeMarking', s.freeMarking),
    say('unmarking', s.unmarking),
    'Undo always takes your card back to how it was at the last ball',
    // A card with no centre square (4 × 4, FR-26a) has no free space whatever the setting.
    say('freeSpace', game.card.freeCell === null ? 'None' : s.freeSpace),
    s.autoFillAvailable
      ? `Auto-fill is available, and ${autoFillOn ? 'on' : 'off'} for you`
      : 'Auto-fill is not available',
    say('requireMarksForClaims', s.requireMarksForClaims),
    say('multipleWins', s.multipleWins),
    say('showBallToPlayers', s.showBallToPlayers),
    say('highlightMatches', s.highlightMatches),
    say('showCallBoard', s.showCallBoard),
  ]
}

/**
 * **Patterns and current rules** (FR-48), opened by tapping the pattern name: two tabs, so a player who
 * missed an announcement can catch up. Patterns are drawn from the masks the server sends.
 */
export function RulesPanel({
  game,
  autoFillOn,
  onClose,
}: {
  game: Game
  autoFillOn: boolean
  onClose: () => void
}) {
  const [tab, setTab] = useState<'patterns' | 'rules'>('patterns')
  const earned = game.settings.freeSpace === 'Earned' && game.card.freeCell !== null

  const tabButton = (value: typeof tab, label: string) => (
    <button
      type="button"
      role="tab"
      id={`tab-${value}`}
      aria-selected={tab === value}
      aria-controls={`panel-${value}`}
      onClick={() => {
        setTab(value)
      }}
      className={`min-h-12 flex-1 rounded-xl px-3 font-semibold focus-visible:outline-4 focus-visible:outline-accent ${tab === value ? 'bg-accent text-accent-ink' : 'text-ink-muted hover:bg-accent-soft'}`}
    >
      {label}
    </button>
  )

  return (
    <Panel title="How to win">
      <div
        role="tablist"
        aria-label="How to win"
        className="mb-3 flex gap-1 rounded-xl border border-line p-0.5"
      >
        {tabButton('patterns', 'Patterns')}
        {tabButton('rules', 'Current rules')}
      </div>

      {tab === 'patterns' ? (
        <div
          role="tabpanel"
          id="panel-patterns"
          aria-labelledby="tab-patterns"
          className="flex flex-col gap-3"
        >
          <p className="text-lg font-bold">{selectionName(game.winCondition, game)}</p>
          {selectedPatterns(game.winCondition, game).length > 1 && (
            <p className="text-sm">Any one of these wins:</p>
          )}
          <div className="flex flex-wrap gap-4">
            {selectedPatterns(game.winCondition, game)
              .flatMap(examples)
              .map((example) => (
                <figure key={example.label} className="flex flex-col items-center gap-1">
                  <PatternGrid
                    mask={example.mask}
                    columns={game.card.columns}
                    rows={game.card.rows}
                    freeCell={game.card.freeCell}
                    earned={earned}
                    label={example.label}
                  />
                  <figcaption className="max-w-32 text-center text-sm text-ink-muted">
                    {example.label}
                  </figcaption>
                </figure>
              ))}
          </div>
          {earned && (
            <p className="text-sm">
              The free space (?) must be earned first: it counts once a called ball isn't on your
              card.
            </p>
          )}
        </div>
      ) : (
        <ul
          role="tabpanel"
          id="panel-rules"
          aria-labelledby="tab-rules"
          className="flex list-disc flex-col gap-1 pl-5"
        >
          {currentRules(game, autoFillOn).map((rule) => (
            <li key={rule}>{rule}</li>
          ))}
        </ul>
      )}

      <Button look="secondary" className="mt-3" onClick={onClose}>
        Close
      </Button>
    </Panel>
  )
}
