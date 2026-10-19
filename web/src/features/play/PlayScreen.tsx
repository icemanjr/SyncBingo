import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { seats } from '../../storage/sessions'
import { normalizeCode } from '../game/codes'
import { useSecondsLeft } from '../game/Countdown'
import { selectionName, winMessage } from '../game/patterns'
import { SelectionGrids } from '../game/SelectionGrids'
import { GameEnded } from '../game/GameEnded'
import { isGone } from '../game/gone'
import { LastBall } from '../game/LastBall'
import { Menu } from '../game/Menu'
import { RulesNotice } from '../game/RulesNotice'
import { useAction } from '../game/useAction'
import { useGame } from '../game/useGame'
import { Button, ErrorText } from '../game/ui'
import { CallBoard } from '../game/CallBoard'
import { Panel } from '../game/ui'
import { Card } from './Card'
import { MarkStylePanel } from './MarkStylePanel'
import { useMarkStyle } from './markStyle'
import { playerApi } from './playerApi'
import { RulesPanel } from './RulesPanel'

/**
 * Every card face in the set, for **Max** text size (FR-51): one size fitted to the hardest face, so cards
 * look the same for everyone. Standard Bingo's faces are its ball numbers; custom sets will send theirs.
 */
function setFaces(board: { balls: number[] }[]): string[] {
  return board.flatMap((group) => group.balls).map(String)
}

/** The board's group labels, one per card column, or none if the groups aren't the card's columns. */
function columnLabels(board: { label: string }[], columns: number): string[] | null {
  const labels = board.map((group) => group.label)
  return labels.length === columns && labels.every((label) => label !== '') ? labels : null
}

/** The card tools row's buttons: an icon over a short label, a full tap target each. */
const toolClass =
  'flex min-h-15 w-full flex-col items-center justify-center gap-1 rounded-2xl bg-surface text-[13px] font-bold text-ink shadow-card focus-visible:outline-4 focus-visible:outline-accent disabled:opacity-50'

const toolIcon = (path: ReactNode) => (
  <svg
    aria-hidden="true"
    width="22"
    height="22"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    className="text-accent-text"
  >
    {path}
  </svg>
)

/** How long "Sent to the host!" shows after a Bingo press. */
const SentForMs = 4000

/**
 * A player's screen (docs/06): their card, marked in their own style (FR-51), the last ball if the host
 * shows it, Undo, the Bingo button during a round (FR-17), and the notices every player gets: rules
 * updated (FR-47), the round's pattern while it counts down, who had Bingo and who was removed or left.
 * **Leave game** asks first (FR-53), and mid-round says they can only join again between rounds.
 */
export function PlayScreen() {
  const code = normalizeCode(useParams().code ?? '')
  const seat = seats.load(code)
  const me = seat?.role === 'player' ? seat : undefined
  const game = useGame(code, me?.token)
  const player = useMemo(() => playerApi(code, me?.token ?? ''), [code, me?.token])
  const [markStyle, setMarkStyle] = useMarkStyle()
  const [styling, setStyling] = useState(false)
  const [open, setOpen] = useState<'rules' | 'board'>()
  const extras = useAction()
  const [sent, setSent] = useState(false)
  const [leaving, setLeaving] = useState(false)
  const leave = useAction()
  const navigate = useNavigate()
  const marking = useAction()
  const bingo = useAction()
  const countdown = useSecondsLeft(
    game.snapshot?.game.state === 'Starting' ? game.snapshot.game.countdownEndsAt : null,
  )

  useEffect(() => {
    if (!sent) return
    const timer = setTimeout(() => {
      setSent(false)
    }, SentForMs)
    return () => {
      clearTimeout(timer)
    }
  }, [sent])

  const removed = game.messages.some(
    (m) => m.type === 'ParticipantRemoved' && (m.data as { name?: string }).name === me?.name,
  )
  // Gone without a removal: the game ended and has expired, so its tokens no longer exist.
  const gone = !removed && isGone(game.error)
  const ended = game.snapshot?.game.state === 'Ended'
  useEffect(() => {
    if (removed || gone || ended) seats.forget(code)
  }, [removed, gone, ended, code])

  if (!me) {
    return (
      <div className="m-auto flex flex-col items-center gap-3 text-center">
        <p>You haven't joined this game on this device.</p>
        <Link to={`/join/${code}`} className="font-semibold text-accent-text underline">
          Join the game
        </Link>
      </div>
    )
  }

  if (removed) {
    return <p className="m-auto text-center">The host removed you from the game.</p>
  }
  if (gone) return <GameEnded />

  if (!game.snapshot) {
    return (
      <p className="m-auto text-ink-muted">
        {game.error ? game.error.detail : 'Loading your card…'}
      </p>
    )
  }

  const { game: view, you } = game.snapshot
  if (view.state === 'Ended') return <GameEnded />

  const card = you?.cards[0]
  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const calls = view.calls
  const last = calls?.at(-1)
  const patternName = selectionName(view.winCondition, view)
  const roundLabel = view.settings.showRoundNumbers ? `Round ${String(view.round)}` : 'This round'
  const roundName = view.settings.showRoundNumbers
    ? `Round ${String(view.upcomingRound)}`
    : 'The next round'
  const wins = game.messages.filter(
    (m) =>
      m.type === 'WinAwarded' || m.type === 'ParticipantRemoved' || m.type === 'ParticipantLeft',
  )

  const toggle = (cell: number, marked: boolean) => {
    if (!card) return
    void marking.run(async () => {
      const result = await player.mark(card.cardId, cell, marked)
      game.patch((s) => ({
        ...s,
        you: s.you && {
          ...s.you,
          cards: s.you.cards.map((c) =>
            c.cardId === card.cardId ? { ...c, marks: result.marks } : c,
          ),
        },
      }))
    })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={() => {
            setOpen(open === 'rules' ? undefined : 'rules')
          }}
          aria-expanded={open === 'rules'}
          className="flex min-h-12 flex-col items-start rounded-xl text-left focus-visible:outline-4 focus-visible:outline-accent"
        >
          <span
            aria-hidden="true"
            className="text-xs font-bold tracking-widest text-ink-muted uppercase"
          >
            {inRound ? `${roundLabel} · how to win` : 'How to win'}
          </span>
          <span className="flex items-center gap-1.5 font-display text-xl font-bold">
            {patternName}
            <svg
              aria-hidden="true"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.4"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="text-accent-text"
            >
              <circle cx="12" cy="12" r="9" />
              <path d="M12 16v-4M12 8h.01" />
            </svg>
          </span>
          <span className="sr-only">: patterns and current rules</span>
        </button>
        {view.settings.showBallToPlayers && last && <LastBall last={last} size="small" />}
      </div>

      {wins.map((message) => {
        const data = message.data as { name: string; announced?: string | null }
        const mine = data.name === me.name
        return (
          <div
            key={message.at}
            role="status"
            className={`flex items-center justify-between gap-2 rounded-2xl p-3 pl-4 font-semibold ${mine ? 'bg-accent font-display text-lg text-accent-ink' : 'border-2 border-notice-line bg-notice text-notice-ink'}`}
          >
            <span>
              {message.type === 'ParticipantRemoved'
                ? `${data.name} was removed from the game`
                : message.type === 'ParticipantLeft'
                  ? `${data.name} left the game`
                  : winMessage(data.name, data.announced, view, mine)}
            </span>
            <Button
              look="quiet"
              onClick={() => {
                game.dismissMessage(message.at)
              }}
            >
              OK
            </Button>
          </div>
        )
      })}

      {open === 'rules' && (
        <RulesPanel
          game={view}
          autoFillOn={you?.autoFillOn ?? false}
          onClose={() => {
            setOpen(undefined)
          }}
        />
      )}

      {game.rulesNotice && (
        <RulesNotice notice={game.rulesNotice} patterns={view} onClose={game.dismissRulesNotice} />
      )}

      {view.state === 'Starting' && (
        <section
          aria-live="polite"
          aria-label="This round"
          className="flex items-center gap-4 rounded-2xl border-2 border-accent bg-accent-soft p-4"
        >
          <SelectionGrids game={view} />
          <div>
            <p className="font-bold">This round: {patternName}</p>
            <p>Starts in {countdown}</p>
          </div>
        </section>
      )}

      {view.state === 'BetweenRounds' && (
        <p className="text-center text-ink-muted">Waiting for the host to start {roundName}.</p>
      )}

      {card && (
        <div className="rounded-3xl bg-surface p-3 shadow-card">
          <Card
            card={card}
            columns={view.card.columns}
            columnLabels={columnLabels(view.board, view.card.columns)}
            freeCell={view.card.freeCell}
            called={calls ? new Set(calls.map((c) => c.number)) : null}
            highlightMatches={view.settings.highlightMatches}
            markStyle={markStyle}
            setFaces={setFaces(view.board)}
            disabled={!inRound || marking.busy}
            onToggle={toggle}
          />
        </div>
      )}
      {marking.error && <ErrorText>{marking.error}</ErrorText>}

      {inRound && card && (
        <div className="flex flex-col items-center gap-3">
          <button
            type="button"
            disabled={bingo.busy}
            onClick={() =>
              void bingo.run(async () => {
                await player.claim(card.cardId)
                setSent(true)
              })
            }
            className="min-h-18 w-full rounded-[22px] bg-accent font-display text-4xl font-extrabold tracking-wide text-accent-ink shadow-[0_6px_0_var(--accent-strong)] transition-transform focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent motion-safe:active:translate-y-1 motion-safe:active:shadow-[0_2px_0_var(--accent-strong)] disabled:opacity-50"
          >
            BINGO!
          </button>
          <p aria-live="polite" className="min-h-6 font-semibold">
            {sent ? 'Sent to the host!' : ''}
          </p>
          {bingo.error && <ErrorText>{bingo.error}</ErrorText>}
        </div>
      )}

      <nav aria-label="Card tools" className="grid auto-cols-fr grid-flow-col gap-1.5">
        {view.settings.showCallBoard && calls && (
          <button
            type="button"
            aria-expanded={open === 'board'}
            onClick={() => {
              setOpen(open === 'board' ? undefined : 'board')
            }}
            className={toolClass}
          >
            {toolIcon(
              <>
                <circle cx="6" cy="6" r="2.5" />
                <circle cx="18" cy="6" r="2.5" />
                <circle cx="6" cy="18" r="2.5" />
                <circle cx="18" cy="18" r="2.5" />
                <circle cx="12" cy="12" r="2.5" />
              </>,
            )}
            Call board
          </button>
        )}
        {inRound && card && (
          <button
            type="button"
            disabled={marking.busy}
            onClick={() =>
              void marking.run(async () => {
                const result = await player.undo(card.cardId)
                game.patch((s) => ({
                  ...s,
                  you: s.you && {
                    ...s.you,
                    cards: s.you.cards.map((c) =>
                      c.cardId === card.cardId ? { ...c, marks: result.marks } : c,
                    ),
                  },
                }))
              })
            }
            className={toolClass}
          >
            {toolIcon(
              <>
                <path d="M9 14L4 9l5-5" />
                <path d="M4 9h11a5 5 0 0 1 0 10h-3" />
              </>,
            )}
            Undo
          </button>
        )}
        <button
          type="button"
          aria-expanded={styling}
          onClick={() => {
            setStyling(!styling)
          }}
          className={toolClass}
        >
          {toolIcon(
            <>
              <circle cx="12" cy="12" r="9" />
              <circle cx="12" cy="12" r="4" fill="currentColor" />
            </>,
          )}
          My marks
        </button>
        <Menu
          label="More"
          above
          triggerClassName={toolClass}
          icon={
            <>
              {toolIcon(
                <>
                  <circle cx="5" cy="12" r="1.5" fill="currentColor" />
                  <circle cx="12" cy="12" r="1.5" fill="currentColor" />
                  <circle cx="19" cy="12" r="1.5" fill="currentColor" />
                </>,
              )}
              More
            </>
          }
          items={[
            {
              label: 'Leave game',
              danger: true,
              onSelect: () => {
                setLeaving(true)
              },
            },
          ]}
        />
      </nav>

      <div className="flex flex-wrap justify-center gap-2">
        {view.settings.autoFillAvailable && you && (
          <label className="flex min-h-12 items-center gap-2 rounded-xl border-2 border-line-soft px-3 font-medium">
            <input
              type="checkbox"
              checked={you.autoFillOn}
              disabled={extras.busy}
              onChange={(e) => {
                const on = e.target.checked
                void extras.run(async () => {
                  await player.setAutoFill(on)
                  game.patch((s) => ({ ...s, you: s.you && { ...s.you, autoFillOn: on } }))
                })
              }}
              className="h-5 w-5"
            />
            Auto-fill missed numbers
          </label>
        )}
        {view.state === 'BetweenRounds' &&
          view.settings.redrawPolicy === 'ParticipantsMayRedraw' &&
          card && (
            <Button
              look="secondary"
              disabled={extras.busy}
              onClick={() =>
                void extras.run(async () => {
                  await player.redraw(card.cardId)
                  await game.refresh()
                })
              }
            >
              New card
            </Button>
          )}
      </div>
      {extras.error && <ErrorText>{extras.error}</ErrorText>}

      {open === 'board' && calls && (
        <Panel title="Call board">
          <CallBoard board={view.board} calls={calls} />
        </Panel>
      )}

      {view.leaderboard && view.leaderboardVisible && (
        <Panel title="Leaderboard">
          {view.leaderboard.length === 0 ? (
            <p className="text-ink-muted">No wins yet.</p>
          ) : (
            <ol className="flex flex-col gap-1">
              {view.leaderboard.map((entry, i) => (
                <li
                  key={entry.participantId}
                  className={`flex justify-between ${entry.name === me.name ? 'font-bold' : ''}`}
                >
                  <span>
                    {i + 1}. {entry.name}
                  </span>
                  <span className="tabular-nums">
                    {entry.wins} win{entry.wins === 1 ? '' : 's'}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </Panel>
      )}

      {styling && (
        <MarkStylePanel
          style={markStyle}
          onChange={setMarkStyle}
          onClose={() => {
            setStyling(false)
          }}
        />
      )}

      {leaving && (
        <div role="alertdialog" aria-label="Leave the game" className="flex flex-col gap-2">
          <p className="font-semibold">
            {inRound || view.state === 'Starting'
              ? 'Leave the game? A round is in progress, so you can only join again between rounds.'
              : 'Leave the game?'}
          </p>
          <div className="flex flex-wrap gap-2">
            <Button
              look="danger"
              disabled={leave.busy}
              onClick={() =>
                void leave.run(async () => {
                  await player.leave()
                  seats.forget(code)
                  void navigate('/')
                })
              }
            >
              Leave
            </Button>
            <Button
              look="secondary"
              onClick={() => {
                setLeaving(false)
              }}
            >
              Stay
            </Button>
          </div>
          {leave.error && <ErrorText>{leave.error}</ErrorText>}
        </div>
      )}
    </div>
  )
}
