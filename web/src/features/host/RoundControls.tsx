import { useEffect, useState } from 'react'
import type { Game } from '../game/gameEvents'
import { useSecondsLeft } from '../game/Countdown'
import { LastBall } from '../game/LastBall'
import { selectionName } from '../game/patterns'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'

/** How long "Dealt ✓" shows after dealing new cards, so a double tap deals once. */
const DealtForMs = 1500

/**
 * The lobby's next-round card (docs/03 §2): the patterns (Change opens Options), how the round starts
 * (Manual or Auto), **Start Round N** and **Deal New Cards** (FR-34); during the 10-second countdown,
 * the seconds left and Cancel.
 */
export function StartCard({
  game,
  host,
  onChangePatterns,
}: {
  game: Game
  host: HostApi
  onChangePatterns: () => void
}) {
  const [startInAuto, setStartInAuto] = useState(false)
  const [dealt, setDealt] = useState(false)
  const { busy, error, run } = useAction()
  const countdown = useSecondsLeft(game.state === 'Starting' ? game.countdownEndsAt : null)
  const roundName = game.settings.showRoundNumbers
    ? `Round ${String(game.upcomingRound)}`
    : 'New Round'

  useEffect(() => {
    if (!dealt) return
    const timer = setTimeout(() => {
      setDealt(false)
    }, DealtForMs)
    return () => {
      clearTimeout(timer)
    }
  }, [dealt])

  return (
    <section
      aria-label="Next round"
      className="flex flex-col gap-4 rounded-[20px] bg-surface p-5 shadow-card"
    >
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-xs font-bold tracking-widest text-ink-muted uppercase">Patterns</p>
          <p className="font-display text-xl font-bold">{selectionName(game.winCondition, game)}</p>
        </div>
        {game.state === 'BetweenRounds' && (
          <Button look="quiet" onClick={onChangePatterns}>
            Change
          </Button>
        )}
      </div>

      {game.state === 'Starting' ? (
        <div className="flex items-center justify-between gap-4 rounded-2xl bg-accent-soft p-4">
          <p className="font-display text-2xl font-bold" aria-live="polite">
            Round starts in {countdown}
          </p>
          <Button look="outline" disabled={busy} onClick={() => void run(host.cancelCountdown)}>
            Cancel
          </Button>
        </div>
      ) : (
        <>
          <fieldset className="grid grid-cols-2 gap-1 rounded-2xl bg-accent-soft p-1">
            <legend className="sr-only">How the round starts</legend>
            {(['Manual', 'Auto'] as const).map((mode) => {
              const checked = startInAuto === (mode === 'Auto')
              return (
                <label
                  key={mode}
                  className={`flex min-h-12 cursor-pointer items-center justify-center rounded-xl font-semibold has-[:focus-visible]:outline-4 has-[:focus-visible]:outline-accent ${checked ? 'bg-surface text-ink shadow-sm' : 'text-accent-text'}`}
                >
                  <input
                    type="radio"
                    name="start-mode"
                    checked={checked}
                    onChange={() => {
                      setStartInAuto(mode === 'Auto')
                    }}
                    className="sr-only"
                  />
                  {mode}
                </label>
              )
            })}
          </fieldset>
          <Button
            className="min-h-15 font-display text-xl"
            disabled={busy}
            onClick={() => void run(() => host.startCountdown(startInAuto))}
          >
            Start {roundName}
          </Button>
          <Button
            look="secondary"
            disabled={busy || dealt}
            onClick={() =>
              void run(async () => {
                await host.clearCards(true)
                setDealt(true)
              })
            }
          >
            {dealt ? 'Dealt ✓' : 'Deal New Cards'}
          </Button>
        </>
      )}
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}

/**
 * Calling during a round: the last ball large with the few before it, **Grab ball** (waiting out the
 * reading time after a rules change, FR-47) and **Auto mode** in manual mode, **Pause** in auto mode.
 */
export function CallingCard({ game, host }: { game: Game; host: HostApi }) {
  const { busy, error, run } = useAction()
  const reading = useSecondsLeft(game.nextBallFrom)
  const calls = game.calls ?? []
  const before = calls.slice(-5, -1).reverse()

  return (
    <section
      aria-label="Calling"
      className="flex flex-col gap-3 rounded-[20px] bg-surface p-3 shadow-card sm:flex-row sm:items-center sm:gap-7 sm:p-6"
    >
      <div className="flex items-center gap-4 sm:flex-col sm:gap-1.5">
        <p className="hidden text-xs font-bold tracking-widest text-ink-muted uppercase sm:block">
          Last Ball
        </p>
        <LastBall last={calls.at(-1)} size="responsive" />
        <div className="flex flex-col gap-1 text-sm text-ink-muted sm:hidden">
          <span>
            <b className="font-display text-xl text-ink">{game.ballsRemaining}</b> balls left
          </span>
        </div>
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-4">
        {before.length > 0 && (
          <div className="hidden flex-wrap items-center gap-2 sm:flex">
            <span className="text-sm text-ink-muted">Before that</span>
            {before.map((call) => (
              <span
                key={call.sequence}
                className="inline-flex h-8 min-w-11 items-center justify-center rounded-full bg-group px-2.5 text-sm font-bold"
              >
                {call.letter} {call.number}
              </span>
            ))}
          </div>
        )}
        <div className="flex flex-wrap items-center gap-3">
          {game.state === 'Manual' ? (
            <>
              <Button
                className="min-h-14 flex-1 px-7 font-display text-lg sm:flex-none"
                disabled={busy || reading > 0}
                onClick={() => void run(host.grabBall)}
              >
                {reading > 0 ? `Grab ball ${String(reading)}…` : 'Grab ball'}
              </Button>
              <Button
                look="outline"
                className="min-h-14"
                disabled={busy || reading > 0}
                onClick={() => void run(host.startAuto)}
              >
                Auto mode
              </Button>
            </>
          ) : (
            <Button
              look="outline"
              className="min-h-14"
              disabled={busy}
              onClick={() => void run(host.pause)}
            >
              Pause
            </Button>
          )}
          <span className="ml-auto hidden text-right text-sm leading-tight text-ink-muted sm:block">
            <b className="font-display text-2xl text-ink">{game.ballsRemaining}</b>
            <br />
            balls left
          </span>
        </div>
        {error && <ErrorText>{error}</ErrorText>}
      </div>
    </section>
  )
}

/** Ending a round (docs/03): **End round** keeps the cards and calls on screen; **Clear cards** clears them and returns to the lobby. */
export function RoundActions({ game, host }: { game: Game; host: HostApi }) {
  const { busy, error, run } = useAction()
  const inRound = game.state === 'Manual' || game.state === 'Auto'
  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2">
        {inRound && (
          <Button
            look="secondary"
            className="flex-1"
            disabled={busy}
            onClick={() => void run(host.endRound)}
          >
            End round
          </Button>
        )}
        <Button
          look="secondary"
          className="flex-1"
          disabled={busy}
          onClick={() => void run(() => host.clearCards(false))}
        >
          Clear cards
        </Button>
      </div>
      {error && <ErrorText>{error}</ErrorText>}
    </div>
  )
}
