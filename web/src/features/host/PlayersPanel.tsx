import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { Menu } from '../game/Menu'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

const avatarColors = [
  'bg-accent',
  'bg-letter-b',
  'bg-letter-i',
  'bg-letter-g',
  'bg-letter-o',
  'bg-letter-n',
]

/**
 * Who is playing (FR-7, FR-38), compact: each row has the player's card and a ⋯ menu to award a win
 * nobody pressed Bingo for (a shout) or remove them, which asks again so a slip doesn't remove anyone.
 * People who joined during the round are listed apart, under **Waiting** (A3): they count as players and
 * hold their names, and join by themselves when the round ends; the host can remove them too.
 */
export function PlayersPanel({
  players,
  wins,
  inRound,
  lobby,
  host,
  onInspect,
  onLeaderboard,
}: {
  players: Schemas['ParticipantSummary'][]
  wins: Map<string, number>
  inRound: boolean
  /** Between rounds: anyone still waiting is waiting for a card, not for the round to end. */
  lobby: boolean
  host: HostApi
  onInspect: (participantId: string) => void
  onLeaderboard: () => void
}) {
  const [confirming, setConfirming] = useState<string>()
  const { busy, error, run } = useAction()
  const playing = players.filter((player) => !player.waiting)
  const waiting = players.filter((player) => player.waiting)

  const confirmRemove = (player: Schemas['ParticipantSummary']) => (
    <span className="flex items-center gap-1">
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
      <Button
        look="quiet"
        onClick={() => {
          setConfirming(undefined)
        }}
      >
        Keep
      </Button>
    </span>
  )

  return (
    <section
      aria-label="Players"
      className="flex min-h-0 flex-col rounded-[20px] bg-surface p-5 pb-3 shadow-card"
    >
      <div className="mb-2 flex items-baseline justify-between gap-2">
        <h2 className="font-display text-xl font-bold">
          Players <span className="font-medium text-ink-muted">{players.length}</span>
        </h2>
        <Button look="quiet" className="text-accent-text" onClick={onLeaderboard}>
          Leaderboard
        </Button>
      </div>
      {playing.length === 0 && waiting.length === 0 ? (
        <p className="pb-2 text-ink-muted">No one has joined yet. Show them the QR code.</p>
      ) : (
        <ul className="flex min-h-0 flex-col divide-y divide-line-soft overflow-y-auto">
          {playing.map((player, i) => {
            const count = wins.get(player.participantId) ?? 0
            return (
              <li key={player.participantId} className="flex min-h-13 items-center gap-3">
                <span
                  aria-hidden="true"
                  className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-extrabold text-white ${avatarColors[i % avatarColors.length] ?? 'bg-accent'}`}
                >
                  {player.name.slice(0, 1).toUpperCase()}
                </span>
                <span className="min-w-0 flex-1 truncate font-semibold">{player.name}</span>
                {count > 0 && (
                  <span className="text-sm text-ink-muted">
                    {count} win{count === 1 ? '' : 's'}
                  </span>
                )}
                {confirming === player.participantId ? (
                  confirmRemove(player)
                ) : (
                  <>
                    <button
                      type="button"
                      aria-label={`See ${player.name}'s card`}
                      onClick={() => {
                        onInspect(player.participantId)
                      }}
                      className="flex min-h-12 min-w-12 items-center justify-center rounded-xl text-accent-text hover:bg-accent-soft focus-visible:outline-4 focus-visible:outline-accent"
                    >
                      <svg
                        aria-hidden="true"
                        width="20"
                        height="20"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      >
                        <rect x="3" y="3" width="18" height="18" rx="3" />
                        <path d="M3 9h18M3 15h18M9 3v18M15 3v18" />
                      </svg>
                    </button>
                    <Menu
                      label={`More for ${player.name}`}
                      items={[
                        ...(inRound
                          ? [
                              {
                                label: 'Award a win',
                                onSelect: () => void run(() => host.award(player.participantId)),
                              },
                            ]
                          : []),
                        {
                          label: `Remove ${player.name}`,
                          danger: true,
                          onSelect: () => {
                            setConfirming(player.participantId)
                          },
                        },
                      ]}
                    />
                  </>
                )}
              </li>
            )
          })}
        </ul>
      )}
      {waiting.length > 0 && (
        <section
          aria-label="Waiting"
          className="mt-2 flex flex-col gap-1 border-t-2 border-line-soft pt-2"
        >
          <h3 className="font-display font-bold">
            Waiting <span className="font-medium text-ink-muted">{waiting.length}</span>
          </h3>
          <p className="text-sm text-ink-muted">
            {!lobby
              ? 'They arrived during the round and get a card as soon as the game is back in the lobby (End round, Clear cards or Cancel).'
              : 'Every different card is in use. Add entries to the set to make more.'}
          </p>
          <ul className="flex flex-col divide-y divide-line-soft">
            {waiting.map((player) => (
              <li key={player.participantId} className="flex min-h-12 items-center gap-3">
                <span className="min-w-0 flex-1 truncate">{player.name}</span>
                {confirming === player.participantId ? (
                  confirmRemove(player)
                ) : (
                  <Button
                    look="quiet"
                    aria-label={`Remove ${player.name}`}
                    onClick={() => {
                      setConfirming(player.participantId)
                    }}
                  >
                    Remove
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}
