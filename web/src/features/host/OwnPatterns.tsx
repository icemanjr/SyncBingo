import { useState } from 'react'
import type { Game } from '../game/gameEvents'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

/** The most patterns a host may draw for one game (the server's limit, FR-21a). */
const MaxOwnPatterns = 5
/** The longest name for a drawn pattern. */
const MaxNameLength = 20

/** The host's own patterns have ids "custom-1", "custom-2", … */
const isOwnPattern = (id: string) => id.startsWith('custom-')

/**
 * The host's own patterns (FR-21a), in Options: each can be renamed (patterns can't be deleted, so a mistake
 * is renamed instead, e.g. "Do not use"), and **Draw a pattern** adds one on the card in use. Adding takes
 * effect at once, after a confirmation, since it can't be undone; choosing a pattern is still an Apply.
 */
export function OwnPatterns({
  game,
  host,
  sameCard,
}: {
  game: Game
  host: HostApi
  /** The draft's card size is the game's: patterns are drawn on the card in use. */
  sameCard: boolean
}) {
  const own = game.patterns.filter((pattern) => isOwnPattern(pattern.id))
  const total = game.cardSizes
    .flatMap((size) => size.patterns)
    .filter((pattern) => isOwnPattern(pattern.id)).length
  const [drawing, setDrawing] = useState(false)

  return (
    <div className="flex flex-col gap-2 rounded-xl border-2 border-line-soft p-2">
      <p className="font-semibold">Your patterns</p>
      {own.length === 0 && !drawing && (
        <p className="text-sm text-ink-muted">Draw your own: tap the squares it needs.</p>
      )}
      {own.map((pattern) => (
        <RenameRow key={pattern.id} id={pattern.id} name={pattern.name} host={host} />
      ))}
      {drawing ? (
        <Drawing
          game={game}
          host={host}
          onDone={() => {
            setDrawing(false)
          }}
        />
      ) : total >= MaxOwnPatterns ? (
        <p className="text-sm text-ink-muted">
          A game can have up to {MaxOwnPatterns} of your own patterns.
        </p>
      ) : !sameCard ? (
        <p className="text-sm text-ink-muted">Apply the new card size to draw on it.</p>
      ) : (
        <Button
          look="secondary"
          onClick={() => {
            setDrawing(true)
          }}
        >
          Draw a pattern
        </Button>
      )}
    </div>
  )
}

/** One of the host's patterns, with **Rename**. */
function RenameRow({ id, name, host }: { id: string; name: string; host: HostApi }) {
  const [renaming, setRenaming] = useState(false)
  const [value, setValue] = useState(name)
  const action = useAction()

  if (!renaming)
    return (
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span>{name}</span>
        <Button
          look="quiet"
          aria-label={`Rename ${name}`}
          onClick={() => {
            setValue(name)
            setRenaming(true)
          }}
        >
          Rename
        </Button>
      </div>
    )

  return (
    <form
      className="flex flex-col gap-2"
      onSubmit={(e) => {
        e.preventDefault()
        void action.run(async () => {
          await host.renamePattern(id, value)
          setRenaming(false)
        })
      }}
    >
      <label className="flex flex-col gap-1 text-sm font-semibold">
        New name for {name}
        <input
          className="min-h-12 rounded-xl border-2 border-line bg-surface px-3"
          value={value}
          maxLength={MaxNameLength * 3}
          onChange={(e) => {
            setValue(e.target.value)
          }}
        />
      </label>
      <div className="flex gap-2">
        <Button type="submit" look="secondary" disabled={action.busy || !value.trim()}>
          Save
        </Button>
        <Button
          look="quiet"
          onClick={() => {
            setRenaming(false)
          }}
        >
          Cancel
        </Button>
      </div>
      {action.error && <ErrorText>{action.error}</ErrorText>}
    </form>
  )
}

/** Draw a pattern: a name and the squares it needs, on a grid like the card in use, then confirm. */
function Drawing({ game, host, onDone }: { game: Game; host: HostApi; onDone: () => void }) {
  const { rows, columns, freeCell } = game.card
  const [name, setName] = useState('')
  const [cells, setCells] = useState<Set<number>>(new Set())
  const [confirming, setConfirming] = useState(false)
  const action = useAction()
  const toggle = (cell: number) => {
    setConfirming(false)
    setCells((current) => {
      const next = new Set(current)
      if (next.has(cell)) next.delete(cell)
      else next.add(cell)
      return next
    })
  }
  const enough = name.trim() !== '' && [...cells].some((cell) => cell !== freeCell)

  return (
    <div className="flex flex-col gap-2">
      <label className="flex flex-col gap-1 text-sm font-semibold">
        Pattern name
        <input
          className="min-h-12 rounded-xl border-2 border-line bg-surface px-3"
          value={name}
          maxLength={MaxNameLength * 3}
          placeholder="Plus sign"
          onChange={(e) => {
            setConfirming(false)
            setName(e.target.value)
          }}
        />
      </label>
      <div
        role="group"
        aria-label="Squares the pattern needs"
        className="grid w-fit gap-1"
        style={{ gridTemplateColumns: `repeat(${String(columns)}, 3rem)` }}
      >
        {Array.from({ length: rows * columns }, (_, cell) => {
          const on = cells.has(cell)
          const row = Math.floor(cell / columns) + 1
          const column = (cell % columns) + 1
          return (
            <button
              key={cell}
              type="button"
              aria-pressed={on}
              aria-label={`Row ${String(row)}, column ${String(column)}${cell === freeCell ? ' (free space)' : ''}`}
              onClick={() => {
                toggle(cell)
              }}
              className={`h-12 w-12 rounded-lg border-2 font-bold focus-visible:outline-4 focus-visible:outline-accent ${
                on ? 'border-accent bg-accent text-accent-ink' : 'border-line bg-surface'
              }`}
            >
              {cell === freeCell ? '★' : on ? '✓' : ''}
            </button>
          )
        })}
      </div>
      {confirming ? (
        <div className="flex flex-col gap-2 rounded-xl bg-notice p-2 text-notice-ink">
          <p className="text-sm font-semibold">
            Add “{name.trim()}”? You can rename it later, but not delete it.
          </p>
          <div className="flex flex-wrap gap-2">
            <Button
              disabled={action.busy}
              onClick={() =>
                void action.run(async () => {
                  await host.addPattern(name, [...cells])
                  onDone()
                })
              }
            >
              Add pattern
            </Button>
            <Button
              look="quiet"
              onClick={() => {
                setConfirming(false)
              }}
            >
              Keep drawing
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2">
          <Button
            look="secondary"
            disabled={!enough}
            onClick={() => {
              setConfirming(true)
            }}
          >
            Add this pattern
          </Button>
          <Button look="quiet" onClick={onDone}>
            Cancel
          </Button>
        </div>
      )}
      {action.error && <ErrorText>{action.error}</ErrorText>}
    </div>
  )
}
