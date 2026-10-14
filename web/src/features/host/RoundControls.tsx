import { useState } from 'react'
import type { Game } from '../game/gameEvents'
import { useSecondsLeft } from '../game/Countdown'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

/**
 * The calling controls for each state (docs/03 §2): Start Round N with its Manual/Auto toggle in the lobby,
 * Cancel during the countdown, Grab ball / Auto mode in manual mode, Pause in auto mode, and End round /
 * Clear cards during a round. Grab ball waits out the reading time after a rules change (FR-47).
 */
export function RoundControls({ game, host }: { game: Game; host: HostApi }) {
  const [startInAuto, setStartInAuto] = useState(false)
  const [redraw, setRedraw] = useState(false)
  const { busy, error, run } = useAction()
  const countdown = useSecondsLeft(game.state === 'Starting' ? game.countdownEndsAt : null)
  const reading = useSecondsLeft(game.nextBallFrom)
  const roundName = game.settings.showRoundNumbers
    ? `Round ${String(game.upcomingRound)}`
    : 'New Round'
  const inRound = game.state === 'Manual' || game.state === 'Auto'

  return (
    <div className="flex flex-col gap-3">
      {game.state === 'BetweenRounds' && (
        <div className="flex flex-wrap items-center gap-3">
          <Button disabled={busy} onClick={() => void run(() => host.startCountdown(startInAuto))}>
            Start {roundName}
          </Button>
          <fieldset className="flex gap-3">
            <legend className="sr-only">How the round starts</legend>
            {(['Manual', 'Auto'] as const).map((mode) => (
              <label key={mode} className="flex min-h-12 items-center gap-2">
                <input
                  type="radio"
                  name="start-mode"
                  checked={startInAuto === (mode === 'Auto')}
                  onChange={() => {
                    setStartInAuto(mode === 'Auto')
                  }}
                  className="h-5 w-5"
                />
                {mode}
              </label>
            ))}
          </fieldset>
        </div>
      )}

      {game.state === 'Starting' && (
        <div className="flex items-center gap-4">
          <p className="text-2xl font-bold" aria-live="polite">
            Round starts in {countdown}
          </p>
          <Button look="secondary" disabled={busy} onClick={() => void run(host.cancelCountdown)}>
            Cancel
          </Button>
        </div>
      )}

      {game.state === 'Manual' && (
        <div className="flex flex-wrap gap-3">
          <Button disabled={busy || reading > 0} onClick={() => void run(host.grabBall)}>
            {reading > 0 ? `Grab ball ${String(reading)}…` : 'Grab ball'}
          </Button>
          <Button
            look="secondary"
            disabled={busy || reading > 0}
            onClick={() => void run(host.startAuto)}
          >
            Auto mode
          </Button>
        </div>
      )}

      {game.state === 'Auto' && (
        <Button look="secondary" disabled={busy} onClick={() => void run(host.pause)}>
          Pause
        </Button>
      )}

      {(inRound || game.state === 'BetweenRounds') && (
        <div className="flex flex-wrap items-center gap-3 border-t border-line-soft pt-3">
          {inRound && (
            <Button look="quiet" disabled={busy} onClick={() => void run(host.endRound)}>
              End round
            </Button>
          )}
          <Button
            look="secondary"
            disabled={busy}
            onClick={() => void run(() => host.clearCards(redraw))}
          >
            Clear cards
          </Button>
          <label className="flex min-h-12 items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={redraw}
              onChange={(e) => {
                setRedraw(e.target.checked)
              }}
              className="h-5 w-5"
            />
            New cards for everyone
          </label>
        </div>
      )}

      {error && <ErrorText>{error}</ErrorText>}
    </div>
  )
}
