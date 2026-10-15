import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { seats } from '../../storage/sessions'
import { CallBoard } from '../game/CallBoard'
import { normalizeCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { useGame } from '../game/useGame'
import { Button, ErrorText, Panel } from '../game/ui'
import { RulesNotice } from '../game/RulesNotice'
import { CardInspector } from './CardInspector'
import { ClaimsPanel } from './ClaimsPanel'
import { DisplayMenu } from './DisplayMenu'
import { hostApi } from './hostApi'
import { JoinPanel } from './JoinPanel'
import { LeaderboardPanel } from './LeaderboardPanel'
import { OptionsPanel } from './OptionsPanel'
import { PlayersPanel } from './PlayersPanel'
import { RoundControls } from './RoundControls'

/**
 * The host console (docs/06): the call board by default, with the calling controls, Bingo claims and
 * detections, players, alerts, the leaderboard, and two menus: **Options** (rules, as a draft applied in one
 * step; hidden in auto mode, FR-32) and **Open display ⧉** (displays, TVs, the join note).
 */
export function HostConsole() {
  const code = normalizeCode(useParams().code ?? '')
  const seat = seats.load(code)
  const token = seat?.role === 'host' ? seat.token : undefined
  const game = useGame(code, token)
  const host = useMemo(() => hostApi(code, token ?? ''), [code, token])
  const [inspecting, setInspecting] = useState<string>()
  const [confirmEnd, setConfirmEnd] = useState(false)
  const [open, setOpen] = useState<'options' | 'displays'>()
  const end = useAction()

  if (!token) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
        <p>This browser isn't hosting that game.</p>
        <Link to="/host/new" className="font-semibold text-accent-text underline">
          Host a new game
        </Link>
      </div>
    )
  }

  if (!game.snapshot) {
    return (
      <p className="m-auto text-ink-muted">
        {game.error ? game.error.detail : 'Loading the game…'}
      </p>
    )
  }

  const { game: view, host: hostView } = game.snapshot
  if (view.state === 'Ended') {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
        <h1 className="text-2xl font-bold">The game has ended</h1>
        <Link to="/" className="font-semibold text-accent-text underline">
          Back to the start
        </Link>
      </div>
    )
  }

  const calls = view.calls ?? []
  const called = new Set(calls.map((call) => call.number))
  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const pattern = view.patterns.find((p) => p.id === view.winCondition)?.name ?? view.winCondition
  const roundLabel = view.settings.showRoundNumbers ? `Round ${String(view.round)}` : 'This round'
  const detections = game.messages
    .filter((m) => m.type === 'WinDetected')
    .map((m) => ({
      at: m.at,
      data: m.data as {
        participantId: string
        name: string
        winConditionId: string
        matches: number
      },
    }))
  const passwordAlerts = game.messages.filter((m) => m.type === 'WrongPasswordsAlert')
  const departures = game.messages.filter((m) => m.type === 'ParticipantLeft')

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-3 rounded-2xl bg-surface p-4 shadow-sm">
        <JoinPanel code={code} joinNote={view.joinNote} />
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="font-semibold text-ink">
            {inRound
              ? `${roundLabel} · ${pattern}${view.state === 'Auto' ? ' · ▶ Auto' : ''}`
              : `Lobby · next: ${pattern}`}
          </p>
          <span className="flex gap-2">
            <Button
              look="secondary"
              onClick={() => {
                setOpen(open === 'displays' ? undefined : 'displays')
              }}
            >
              Open display ⧉
            </Button>
            {view.state !== 'Auto' && (
              <Button
                look="secondary"
                onClick={() => {
                  setOpen(open === 'options' ? undefined : 'options')
                }}
              >
                Options
              </Button>
            )}
          </span>
        </div>
      </header>

      {game.rulesNotice && (
        <RulesNotice
          notice={game.rulesNotice}
          patterns={view.patterns}
          onClose={game.dismissRulesNotice}
        />
      )}

      {open === 'options' && view.state !== 'Auto' && hostView && (
        <OptionsPanel
          game={view}
          settings={hostView.settings}
          host={host}
          onClose={() => {
            setOpen(undefined)
          }}
        />
      )}
      {open === 'displays' && (
        <DisplayMenu
          host={host}
          joinNote={view.joinNote}
          onClose={() => {
            setOpen(undefined)
          }}
        />
      )}

      {passwordAlerts.map((alert) => {
        const data = alert.data as { wrongInLastMinute: number; lockedFor: string | null }
        return (
          <div
            key={alert.at}
            role="alert"
            className="flex items-start justify-between gap-3 rounded-2xl bg-danger-soft p-4 text-danger-ink"
          >
            <p>
              {data.lockedFor
                ? 'Joining is locked for 10 minutes from one network after too many wrong passwords.'
                : `${String(data.wrongInLastMinute)} wrong passwords in the last minute. Check the password you shared.`}
            </p>
            <Button
              look="quiet"
              onClick={() => {
                game.dismissMessage(alert.at)
              }}
            >
              OK
            </Button>
          </div>
        )
      })}

      {departures.map((note) => (
        <div
          key={note.at}
          role="status"
          className="flex items-center justify-between gap-3 rounded-2xl bg-notice p-3"
        >
          <p>{(note.data as { name: string }).name} left the game</p>
          <Button
            look="quiet"
            onClick={() => {
              game.dismissMessage(note.at)
            }}
          >
            OK
          </Button>
        </div>
      ))}

      <ClaimsPanel
        claims={hostView?.pendingClaims ?? []}
        detections={detections}
        everyDeclaredWin={hostView?.settings.multipleWins === 'EveryDeclaredWin'}
        host={host}
        onInspect={setInspecting}
        onDismissDetection={game.dismissMessage}
      />

      {inspecting && (
        <CardInspector
          participantId={inspecting}
          called={called}
          columns={view.card.columns}
          host={host}
          onClose={() => {
            setInspecting(undefined)
          }}
        />
      )}

      <Panel title="Calling">
        <RoundControls game={view} host={host} />
      </Panel>

      <Panel title="Call board">
        <CallBoard board={view.board} calls={calls} />
      </Panel>

      <PlayersPanel
        players={hostView?.participants ?? []}
        inRound={inRound}
        host={host}
        onInspect={setInspecting}
      />

      <LeaderboardPanel
        entries={view.leaderboard ?? []}
        shown={view.leaderboardVisible}
        host={host}
      />

      <div className="flex justify-end">
        {confirmEnd ? (
          <span className="flex flex-wrap items-center gap-2">
            <span>End the game for everyone?</span>
            <Button look="danger" disabled={end.busy} onClick={() => void end.run(host.endGame)}>
              End game
            </Button>
            <Button
              look="quiet"
              onClick={() => {
                setConfirmEnd(false)
              }}
            >
              Keep playing
            </Button>
          </span>
        ) : (
          <Button
            look="quiet"
            onClick={() => {
              setConfirmEnd(true)
            }}
          >
            End game…
          </Button>
        )}
      </div>
      {end.error && <ErrorText>{end.error}</ErrorText>}
    </div>
  )
}
