import { useEffect, useState } from 'react'
import type { Schemas } from '../../api/client'
import { cardFace } from '../game/faces'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

/**
 * A player's card for checking a claim (FR-38): what they marked, and which numbers have actually been
 * called, since wins are judged on called numbers (ADR 0008).
 */
export function CardInspector({
  participantId,
  called,
  columns,
  host,
  onClose,
}: {
  participantId: string
  called: Set<number>
  columns: number
  host: HostApi
  onClose: () => void
}) {
  const [player, setPlayer] = useState<Schemas['PlayerView']>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    host.inspect(participantId).then(setPlayer, () => {
      setError('That card could not be loaded.')
    })
  }, [host, participantId])

  return (
    <Panel title={player ? `${player.name}'s card` : 'Card'}>
      {error && <ErrorText>{error}</ErrorText>}
      {player?.cards.map((card) => (
        <div
          key={card.cardId}
          className="grid gap-1"
          style={{
            gridTemplateColumns: `repeat(${String(columns)}, minmax(0, ${card.faces ? '4.5rem' : '3rem'}))`,
          }}
        >
          {card.cells.map((number, cell) => {
            const marked = (card.marks & (1 << cell)) !== 0
            const isCalled = number === 0 || called.has(number)
            return (
              <div
                key={cell}
                aria-label={`${number === 0 ? 'Free' : cardFace(card, cell)}${marked ? ', marked' : ''}${isCalled ? ', called' : ''}`}
                title={number === 0 ? undefined : cardFace(card, cell)}
                className={`flex h-12 items-center justify-center overflow-hidden rounded-lg border-2 px-0.5 text-center leading-tight font-bold tabular-nums ${card.faces ? 'text-[11px] break-words' : ''} ${
                  marked ? 'border-accent-strong bg-accent text-accent-ink' : 'border-line-soft'
                } ${isCalled ? '' : 'opacity-60'}`}
              >
                {number === 0 ? '★' : cardFace(card, cell)}
                {isCalled && !marked && <span className="sr-only"> (called, not marked)</span>}
              </div>
            )
          })}
        </div>
      ))}
      <p className="mt-2 text-sm text-ink-muted">
        Filled: marked by the player. Faded: not called yet.
      </p>
      <Button look="secondary" className="mt-3" onClick={onClose}>
        Close
      </Button>
    </Panel>
  )
}
