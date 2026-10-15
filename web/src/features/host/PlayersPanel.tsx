import { useState } from 'react'
import type { Schemas } from '../../api/client'
import { Menu } from '../game/Menu'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

const avatarColours = [
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
 * In the lobby the players show as chips, as they join.
 */
export function PlayersPanel({
  players,
  wins,
  inRound,
  host,
  onInspect,
  onLeaderboard,
}: {
  players: Schemas['ParticipantSummary'][]
  wins: Map<string, number>
  inRound: boolean
  host: HostApi
  onInspect: (participantId: string) => void
  onLeaderboard: () => void
}) {
  const [confirming, setConfirming] = useState<string>()
  const { busy, error, run } = useAction()

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
      {players.length === 0 ? (
        <p className="pb-2 text-ink-muted">No one has joined yet. Show them the QR code.</p>
      ) : (
        <ul className="flex min-h-0 flex-col divide-y divide-line-soft overflow-y-auto">
          {players.map((player, i) => {
            const count = wins.get(player.participantId) ?? 0
            return (
              <li key={player.participantId} className="flex min-h-13 items-center gap-3">
                <span
                  aria-hidden="true"
                  className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-extrabold text-white ${avatarColours[i % avatarColours.length] ?? 'bg-accent'}`}
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
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}
