import { useEffect, useState } from 'react'
import { usePalette } from '../../app/palettes'
import { useParams } from 'react-router'
import { CallBoard } from '../game/CallBoard'
import { isGone } from '../game/gone'
import { LastBall } from '../game/LastBall'
import { normalizeCode } from '../game/codes'
import { useSecondsLeft } from '../game/Countdown'
import { selectionName, winMessage } from '../game/patterns'
import { SelectionGrids } from '../game/SelectionGrids'
import { RulesNotice } from '../game/RulesNotice'
import { useGame } from '../game/useGame'
import { JoinStage } from '../host/JoinStage'
import { forgetDisplayToken, takeDisplayToken } from './displayToken'

/**
 * The display view (FR-28, the design reference): what the room sees on a projector or TV. No ads, no
 * controls, read-only. On a wide screen, the left column has the round, the last ball large (or the
 * countdown), the patterns under it and the leaderboard when the host shows it. The right has the call board at the top, at full
 * size, or between rounds the **Join Now!** panel with the QR and the password note (FR-49); under it, the
 * latest win, large, and rules notices (FR-47).
 */
export function DisplayView() {
  const code = normalizeCode(useParams().code ?? '')
  const [token] = useState(() => takeDisplayToken(code))
  const game = useGame(code, token)
  usePalette(game.snapshot?.game.settings.theme) // the room's screen shows the game's colours (FR-50a)
  const countdown = useSecondsLeft(
    game.snapshot?.game.state === 'Starting' ? game.snapshot.game.countdownEndsAt : null,
  )
  // Disconnected only when the host said so; a token the server no longer knows means the game expired.
  const disconnected = game.messages.some((m) => m.type === 'DisplayDisconnected')
  const gone = !disconnected && isGone(game.error)

  useEffect(() => {
    if (disconnected || gone) forgetDisplayToken(code)
  }, [disconnected, gone, code])

  const centred = (text: string) => <p className="m-auto p-8 text-center text-3xl">{text}</p>

  if (!token)
    return centred('Open this display from the host’s “Open display” menu, or connect a TV at /tv.')
  if (disconnected) return centred('The host disconnected this display.')
  if (gone) return centred('This game has ended. Thanks for playing!')
  if (!game.snapshot) return centred(game.error ? game.error.detail : 'Connecting…')

  const view = game.snapshot.game
  if (view.state === 'Ended') return centred('The game has ended. Thanks for playing!')

  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const lobby = view.state === 'BetweenRounds'
  const roundLabel = view.settings.showRoundNumbers
    ? `Round ${String(view.upcomingRound)}`
    : inRound
      ? 'This round'
      : 'Next round'
  const latestWin = game.messages.filter((m) => m.type === 'WinAwarded').at(-1)
  const calls = view.calls ?? []

  return (
    <div className="flex min-h-dvh flex-col gap-6 p-6 lg:grid lg:grid-cols-[300px_minmax(0,1fr)] lg:gap-10 lg:p-10">
      <div className="flex flex-col items-center gap-5 lg:items-stretch">
        <div className="flex flex-col items-center gap-2 lg:items-start">
          <span className="rounded-full bg-ink px-4 py-1.5 text-xl font-extrabold text-page">
            {roundLabel}
          </span>
        </div>

        {view.state === 'Starting' ? (
          <div aria-live="polite" className="flex flex-col items-center gap-2 py-6">
            <p className="text-lg font-bold tracking-widest text-ink-muted uppercase">
              Round starts in
            </p>
            <p className="font-display text-9xl font-extrabold tabular-nums">{countdown}</p>
          </div>
        ) : (
          !lobby && (
            <div className="flex flex-col items-center gap-2">
              <p className="text-lg font-bold tracking-widest text-ink-muted uppercase">
                Last Ball
              </p>
              <LastBall last={calls.at(-1)} size="large" />
            </div>
          )
        )}

        <div className="flex flex-col items-center gap-2 lg:items-start">
          <span className="font-display text-3xl font-bold">
            {selectionName(view.winCondition, view)}
          </span>
          <SelectionGrids game={view} />
        </div>

        {view.leaderboardVisible && view.leaderboard && (
          <section
            aria-label="Leaderboard"
            className="rounded-3xl bg-surface p-5 text-xl shadow-card"
          >
            <h2 className="mb-2 font-display text-2xl font-bold">Leaderboard</h2>
            {view.leaderboard.length === 0 ? (
              <p className="text-ink-muted">No wins yet.</p>
            ) : (
              <ol className="flex flex-col gap-1">
                {view.leaderboard.slice(0, 5).map((entry, i) => (
                  <li key={entry.participantId} className="flex justify-between gap-3">
                    <span className="truncate">
                      {i + 1}. {entry.name}
                    </span>
                    <span className="font-bold tabular-nums">{entry.wins}</span>
                  </li>
                ))}
              </ol>
            )}
          </section>
        )}
      </div>

      {/* The board stays at the top, at full size; alerts go under it, never above or beside it. */}
      <div className="flex min-w-0 flex-col justify-start gap-6">
        {lobby ? (
          <div className="mx-auto w-full max-w-4xl">
            <JoinStage code={code} joinNote={view.joinNote} />
          </div>
        ) : (
          <section aria-label="Call board" className="rounded-[28px] bg-surface p-6 shadow-card">
            <CallBoard
              board={view.board}
              calls={calls}
              content={view.content}
              big
              showLastBall={false}
            />
          </section>
        )}
        {latestWin && (
          <p
            key={latestWin.at}
            role="status"
            className={`rounded-3xl bg-accent px-6 py-5 text-center font-display text-5xl font-extrabold text-accent-ink ${view.settings.winHighlight ? 'ring-8 ring-ball/60' : ''}`}
          >
            {winMessage(
              (latestWin.data as { name: string }).name,
              (latestWin.data as { announced?: string | null }).announced,
              view,
              false,
            )}
          </p>
        )}
        {game.rulesNotice && (
          <RulesNotice
            notice={game.rulesNotice}
            patterns={view}
            onClose={game.dismissRulesNotice}
          />
        )}
      </div>
    </div>
  )
}
