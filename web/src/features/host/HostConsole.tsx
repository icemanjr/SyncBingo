import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { seats } from '../../storage/sessions'
import { CallBoard } from '../game/CallBoard'
import { normalizeCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { useGame } from '../game/useGame'
import { selectionName } from '../game/patterns'
import { Dialog } from '../game/Dialog'
import { GameEnded } from '../game/GameEnded'
import { isGone } from '../game/gone'
import { Menu } from '../game/Menu'
import { Button, ErrorText } from '../game/ui'
import { RulesNotice } from '../game/RulesNotice'
import { CardInspector } from './CardInspector'
import { ClaimsPanel } from './ClaimsPanel'
import { DisplayMenu } from './DisplayMenu'
import { hostApi } from './hostApi'
import { JoinStage } from './JoinStage'
import { LeaderboardPanel } from './LeaderboardPanel'
import { OptionsPanel } from './OptionsPanel'
import { PlayersPanel } from './PlayersPanel'
import { CallingCard, RoundActions, StartCard } from './RoundControls'

/**
 * The host console (docs/06, the design reference). In the lobby: how to join (QR, code, join note) beside
 * the next-round card and the players. During a round on a laptop, everything on one screen: the calling
 * card and the call board, with Bingo calls, the players and the round's end on the right. On a phone:
 * tabs (Board, Bingo!, Players) above a calling bar pinned to the bottom. **Options** (rules, applied in one
 * step; not in auto mode, FR-32), **Display** (displays, TVs, the join note), the leaderboard, a player's
 * card and ending the game open as dialogs.
 */
export function HostConsole() {
  const code = normalizeCode(useParams().code ?? '')
  const seat = seats.load(code)
  const token = seat?.role === 'host' ? seat.token : undefined
  const game = useGame(code, token)
  const host = useMemo(() => hostApi(code, token ?? ''), [code, token])
  const [inspecting, setInspecting] = useState<string>()
  const [open, setOpen] = useState<'options' | 'displays' | 'leaderboard' | 'end'>()
  const [tab, setTab] = useState<'board' | 'bingo' | 'players'>('board')
  const end = useAction()
  // The game is over (ended, or expired so the server no longer knows it): this browser forgets it.
  const gone = isGone(game.error)
  const ended = game.snapshot?.game.state === 'Ended'
  useEffect(() => {
    if (gone || ended) seats.forget(code)
  }, [gone, ended, code])

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

  if (gone) return <GameEnded hostLink />

  if (!game.snapshot) {
    return (
      <p className="m-auto text-ink-muted">
        {game.error ? game.error.detail : 'Loading the game…'}
      </p>
    )
  }

  const { game: view, host: hostView } = game.snapshot
  if (view.state === 'Ended') return <GameEnded hostLink />

  const calls = view.calls ?? []
  const called = new Set(calls.map((call) => call.number))
  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const lobby = !inRound
  const pattern = selectionName(view.winCondition, view)
  const roundLabel = view.settings.showRoundNumbers ? `Round ${String(view.round)}` : 'This round'
  const players = hostView?.participants ?? []
  const claims = hostView?.pendingClaims ?? []
  const wins = new Map((view.leaderboard ?? []).map((e) => [e.participantId, e.wins]))
  const detections = game.messages
    .filter((m) => m.type === 'WinDetected')
    .map((m) => ({
      at: m.at,
      data: m.data as {
        participantId: string
        name: string
        winConditionId: string
        matches: number
        completed?: string[] | null
      },
    }))
  const passwordAlerts = game.messages.filter((m) => m.type === 'WrongPasswordsAlert')
  const departures = game.messages.filter((m) => m.type === 'ParticipantLeft')
  const bingoCount = claims.length + detections.length
  const close = () => {
    setOpen(undefined)
  }
  // On phones one tab shows at a time; on laptops (lg) everything does.
  const shownOn = (which: typeof tab) => (tab === which ? 'flex' : 'hidden') + ' lg:flex'

  const playersPanel = (
    <PlayersPanel
      players={players}
      wins={wins}
      inRound={inRound}
      host={host}
      onInspect={setInspecting}
      onLeaderboard={() => {
        setOpen('leaderboard')
      }}
    />
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="rounded-full bg-ink px-3.5 py-1.5 text-sm font-bold text-page">
          {lobby ? 'Lobby' : `${roundLabel}${view.state === 'Auto' ? ' · Auto' : ''}`}
        </span>
        {view.state === 'Auto' ? (
          <span className="px-2 font-semibold">{pattern}</span>
        ) : (
          <button
            type="button"
            onClick={() => {
              setOpen('options')
            }}
            className="inline-flex min-h-12 items-center gap-2 rounded-full border-2 border-line-soft bg-surface px-4 font-semibold focus-visible:outline-4 focus-visible:outline-accent"
          >
            {pattern}
            <span className="sr-only">: change patterns and rules</span>
            <svg
              aria-hidden="true"
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
            </svg>
          </button>
        )}
        <span className="text-sm text-ink-muted">
          {players.length} player{players.length === 1 ? '' : 's'} ·{' '}
          {view.state === 'BetweenRounds' ? 'joining open' : 'joining closed'}
        </span>
        <span className="ml-auto flex gap-2">
          <Button
            look="secondary"
            onClick={() => {
              setOpen('displays')
            }}
          >
            Display
          </Button>
          {view.state !== 'Auto' && (
            <Button
              look="secondary"
              onClick={() => {
                setOpen('options')
              }}
            >
              Options
            </Button>
          )}
          <Menu
            label="More: leaderboard, end the game"
            items={[
              {
                label: 'Leaderboard',
                onSelect: () => {
                  setOpen('leaderboard')
                },
              },
              {
                label: 'End game…',
                danger: true,
                onSelect: () => {
                  setOpen('end')
                },
              },
            ]}
          />
        </span>
      </div>

      {game.rulesNotice && (
        <RulesNotice notice={game.rulesNotice} patterns={view} onClose={game.dismissRulesNotice} />
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
          className="flex items-center justify-between gap-3 rounded-2xl bg-notice p-3 text-notice-ink"
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

      {lobby ? (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px]">
          <JoinStage code={code} joinNote={view.joinNote} host={host} />
          <div className="flex flex-col gap-4">
            <StartCard
              game={view}
              host={host}
              onChangePatterns={() => {
                setOpen('options')
              }}
            />
            {playersPanel}
            {view.round > 0 && <RoundActions game={view} host={host} />}
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-4 lg:grid lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start">
          <div
            role="tablist"
            aria-label="Host views"
            className="grid grid-cols-3 gap-1 rounded-2xl bg-accent-soft p-1 lg:hidden"
          >
            {(
              [
                ['board', 'Board'],
                ['bingo', 'Bingo!'],
                ['players', 'Players'],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                role="tab"
                aria-selected={tab === value}
                onClick={() => {
                  setTab(value)
                }}
                className={`flex min-h-12 items-center justify-center gap-1.5 rounded-xl font-bold focus-visible:outline-4 focus-visible:outline-accent ${tab === value ? 'bg-surface text-ink shadow-sm' : 'text-accent-text'}`}
              >
                {label}
                {value === 'bingo' && bingoCount > 0 && (
                  <>
                    <span
                      aria-hidden="true"
                      className="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-ball px-1 text-xs font-extrabold text-ball-ink"
                    >
                      {bingoCount}
                    </span>
                    <span className="sr-only">, {bingoCount} waiting</span>
                  </>
                )}
              </button>
            ))}
          </div>

          {/* Phones: one column, the calling card pinned to the bottom (last, so it can stick). Laptops: two. */}
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className="sticky bottom-0 z-20 order-last -mx-4 bg-page px-4 pt-2 pb-3 lg:static lg:order-none lg:mx-0 lg:bg-transparent lg:p-0">
              <CallingCard game={view} host={host} />
            </div>
            <section
              aria-label="Call board"
              className={`${shownOn('board')} flex-col gap-3 rounded-[20px] bg-surface p-4 shadow-card lg:p-6`}
            >
              <div className="flex items-baseline justify-between">
                <h2 className="font-display text-xl font-bold">Call board</h2>
                <span className="text-sm text-ink-muted">
                  {calls.length} of {calls.length + view.ballsRemaining} called
                </span>
              </div>
              <CallBoard
                board={view.board}
                calls={calls}
                content={view.content}
                showLastBall={false}
              />
            </section>
          </div>
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className={`${shownOn('bingo')} flex-col gap-4`}>
              <ClaimsPanel
                claims={claims}
                detections={detections}
                everyDeclaredWin={hostView?.settings.multipleWins === 'EveryDeclaredWin'}
                patterns={view}
                host={host}
                onInspect={setInspecting}
                onDismissDetection={game.dismissMessage}
              />
              {bingoCount === 0 && (
                <p className="rounded-[20px] bg-surface p-5 text-ink-muted shadow-card lg:hidden">
                  No Bingo calls yet.
                </p>
              )}
            </div>
            <div className={`${shownOn('players')} flex-col gap-4`}>{playersPanel}</div>
            <RoundActions game={view} host={host} />
          </div>
        </div>
      )}

      {open === 'options' && view.state !== 'Auto' && hostView && (
        <Dialog label="Options" onClose={close} dismissible={false}>
          <OptionsPanel game={view} settings={hostView.settings} host={host} onClose={close} />
        </Dialog>
      )}
      {open === 'displays' && (
        <Dialog label="Displays" onClose={close}>
          <DisplayMenu host={host} joinNote={view.joinNote} onClose={close} />
        </Dialog>
      )}
      {open === 'leaderboard' && (
        <Dialog label="Leaderboard" onClose={close}>
          <LeaderboardPanel
            entries={view.leaderboard ?? []}
            shown={view.leaderboardVisible}
            host={host}
          />
          <div className="flex justify-end px-3 pb-2">
            <Button look="quiet" onClick={close}>
              Close
            </Button>
          </div>
        </Dialog>
      )}
      {open === 'end' && (
        <Dialog label="End the game" onClose={close}>
          <section className="flex flex-col gap-4 rounded-[20px] bg-surface p-5">
            <h2 className="font-display text-xl font-bold">End the game for everyone?</h2>
            <p className="text-ink-muted">
              Every screen shows that the game has ended. This can't be undone.
            </p>
            <div className="flex flex-wrap gap-2">
              <Button look="danger" disabled={end.busy} onClick={() => void end.run(host.endGame)}>
                End game
              </Button>
              <Button look="quiet" onClick={close}>
                Keep playing
              </Button>
            </div>
            {end.error && <ErrorText>{end.error}</ErrorText>}
          </section>
        </Dialog>
      )}
      {inspecting && (
        <Dialog
          label="A player's card"
          onClose={() => {
            setInspecting(undefined)
          }}
        >
          <CardInspector
            participantId={inspecting}
            called={called}
            columns={view.card.columns}
            host={host}
            onClose={() => {
              setInspecting(undefined)
            }}
          />
        </Dialog>
      )}
    </div>
  )
}
