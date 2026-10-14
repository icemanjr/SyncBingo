import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { useAction } from '../game/useAction'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

/**
 * Who is playing (FR-7, FR-38): the host can award a win nobody pressed Bingo for (a shout) and remove a
 * player, with a second tap to confirm so a slip doesn't remove anyone.
 */
export function PlayersPanel({
  players,
  inRound,
  host,
  onInspect,
}: {
  players: Schemas['ParticipantSummary'][]
  inRound: boolean
  host: HostApi
  onInspect: (participantId: string) => void
}) {
  const [confirming, setConfirming] = useState<string>()
  const { busy, error, run } = useAction()

  return (
    <Panel title={`Players (${String(players.length)})`}>
      {players.length === 0 ? (
        <p className="text-slate-600">No one has joined yet. Show them the QR code.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-slate-100">
          {players.map((player) => (
            <li
              key={player.participantId}
              className="flex flex-wrap items-center justify-between gap-2 py-2"
            >
              <span className="font-medium">{player.name}</span>
              <span className="flex flex-wrap gap-2">
                <Button
                  look="quiet"
                  onClick={() => {
                    onInspect(player.participantId)
                  }}
                >
                  Card
                </Button>
                {inRound && (
                  <Button
                    look="secondary"
                    disabled={busy}
                    onClick={() => void run(() => host.award(player.participantId))}
                  >
                    Award win
                  </Button>
                )}
                {confirming === player.participantId ? (
                  <Button
                    look="danger"
                    disabled={busy}
                    onClick={() =>
                      void run(() => host.remove(player.participantId)).then(() => {
                        setConfirming(undefined)
                      })
                    }
                  >
                    Remove {player.name}?
                  </Button>
                ) : (
                  <Button
                    look="quiet"
                    onClick={() => {
                      setConfirming(player.participantId)
                    }}
                  >
                    Remove
                  </Button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
      {error && <ErrorText>{error}</ErrorText>}
    </Panel>
  )
}
