import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { CallBoard } from '../game/CallBoard'
import { normalizeCode } from '../game/codes'
import { useSecondsLeft } from '../game/Countdown'
import { PatternGrid } from '../game/PatternGrid'
import { RulesNotice } from '../game/RulesNotice'
import { useGame } from '../game/useGame'
import { JoinPanel } from '../host/JoinPanel'
import { forgetDisplayToken, takeDisplayToken } from './displayToken'

/**
 * The display view (FR-28): what the room sees on a projector or TV. No ads, no controls, read-only: the
 * round and pattern, the last ball huge, the call board, and between rounds the join QR with the join note
 * (FR-49); plus the countdown, rules notices (FR-47), wins and the leaderboard when the host shows it.
 */
export function DisplayView() {
  const code = normalizeCode(useParams().code ?? '')
  const [token] = useState(() => takeDisplayToken(code))
  const game = useGame(code, token)
  const countdown = useSecondsLeft(
    game.snapshot?.game.state === 'Starting' ? game.snapshot.game.countdownEndsAt : null,
  )
  const disconnected =
    game.messages.some((m) => m.type === 'DisplayDisconnected') || game.error?.status === 401

  useEffect(() => {
    if (disconnected) forgetDisplayToken(code)
  }, [disconnected, code])

  const centred = (text: string) => <p className="m-auto p-8 text-center text-3xl">{text}</p>

  if (!token)
    return centred('Open this display from the host’s “Open display” menu, or connect a TV at /tv.')
  if (disconnected) return centred('The host disconnected this display.')
  if (!game.snapshot) return centred(game.error ? game.error.detail : 'Connecting…')

  const view = game.snapshot.game
  if (view.state === 'Ended') return centred('The game has ended. Thanks for playing!')

  const pattern = view.patterns.find((p) => p.id === view.winCondition)
  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const roundLabel = view.settings.showRoundNumbers
    ? `Round ${String(view.upcomingRound)}`
    : inRound
      ? 'This round'
      : 'Next round'
  const wins = game.messages.filter((m) => m.type === 'WinAwarded')

  return (
    <div className="flex min-h-dvh flex-col gap-6 p-6">
      <header className="flex flex-wrap items-center justify-center gap-4 text-3xl font-bold">
        <span>{roundLabel}</span>
        <span aria-hidden="true">·</span>
        <span>{pattern?.name ?? view.winCondition}</span>
        {pattern && (
          <PatternGrid
            mask={pattern.masks[0] ?? 0}
            columns={view.card.columns}
            rows={view.card.rows}
            freeCell={view.card.freeCell}
            earned={view.settings.freeSpace === 'Earned'}
            label={`${pattern.name} pattern`}
          />
        )}
      </header>

      {wins.map((message) => (
        <p
          key={message.at}
          role="status"
          className="rounded-2xl bg-accent p-4 text-center text-4xl font-black text-accent-ink"
        >
          {view.settings.winHighlight ? '🎉 ' : ''}
          {(message.data as { name: string }).name} had Bingo!
        </p>
      ))}

      {game.rulesNotice && (
        <RulesNotice
          notice={game.rulesNotice}
          patterns={view.patterns}
          onClose={game.dismissRulesNotice}
        />
      )}

      {view.state === 'Starting' && (
        <p aria-live="polite" className="text-center text-5xl font-bold">
          Round starts in {countdown}
        </p>
      )}

      <CallBoard board={view.board} calls={view.calls ?? []} big />

      {!inRound && view.state !== 'Starting' && (
        <section aria-label="Join" className="mx-auto rounded-2xl bg-surface p-6 text-xl">
          <JoinPanel code={code} joinNote={view.joinNote} />
        </section>
      )}

      {view.leaderboardVisible && view.leaderboard && (
        <section
          aria-label="Leaderboard"
          className="mx-auto w-full max-w-xl rounded-2xl bg-surface p-6 text-2xl"
        >
          <h2 className="mb-2 font-bold">Leaderboard</h2>
          <ol>
            {view.leaderboard.map((entry, i) => (
              <li key={entry.participantId} className="flex justify-between">
                <span>
                  {i + 1}. {entry.name}
                </span>
                <span className="tabular-nums">{entry.wins}</span>
              </li>
            ))}
          </ol>
        </section>
      )}
    </div>
  )
}
