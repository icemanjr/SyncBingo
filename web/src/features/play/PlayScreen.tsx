import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { seats } from '../../storage/sessions'
import { normalizeCode } from '../game/codes'
import { useSecondsLeft } from '../game/Countdown'
import { PatternGrid } from '../game/PatternGrid'
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

/** How long "Sent to the host!" shows after a Bingo press. */
const SentForMs = 4000

/**
 * A player's screen (docs/06): their card, marked in their own style (FR-51), the last ball if the host
 * shows it, Undo, the Bingo button during a round (FR-17), and the notices every player gets: rules
 * updated (FR-47), the round's pattern while it counts down, who had Bingo and who was removed.
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
  useEffect(() => {
    if (removed) seats.forget(code)
  }, [removed, code])

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

  if (removed || game.error?.status === 401) {
    return <p className="m-auto text-center">The host removed you from the game.</p>
  }

  if (!game.snapshot) {
    return (
      <p className="m-auto text-ink-muted">
        {game.error ? game.error.detail : 'Loading your card…'}
      </p>
    )
  }

  const { game: view, you } = game.snapshot
  if (view.state === 'Ended') {
    return (
      <p className="m-auto text-center text-lg font-semibold">
        The game has ended. Thanks for playing!
      </p>
    )
  }

  const card = you?.cards[0]
  const inRound = view.state === 'Manual' || view.state === 'Auto'
  const calls = view.calls
  const last = calls?.at(-1)
  const pattern = view.patterns.find((p) => p.id === view.winCondition)
  const earned = view.settings.freeSpace === 'Earned'
  const roundName = view.settings.showRoundNumbers
    ? `Round ${String(view.upcomingRound)}`
    : 'The next round'
  const wins = game.messages.filter(
    (m) => m.type === 'WinAwarded' || m.type === 'ParticipantRemoved',
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
          className="flex min-h-12 items-center gap-1 rounded-xl px-2 font-semibold underline-offset-4 hover:underline focus-visible:outline-4 focus-visible:outline-accent"
        >
          {pattern?.name ?? view.winCondition} <span aria-hidden="true">ⓘ</span>
          <span className="sr-only">: patterns and current rules</span>
        </button>
        {view.settings.showBallToPlayers && last && (
          <p
            aria-live="polite"
            className="rounded-xl bg-accent px-3 py-1 text-2xl font-bold text-accent-ink tabular-nums"
          >
            {last.letter} {last.number}
          </p>
        )}
      </div>

      {wins.map((message) => {
        const data = message.data as { name: string }
        const mine = data.name === me.name
        return (
          <div
            key={message.at}
            role="status"
            className={`flex items-center justify-between gap-2 rounded-2xl p-3 font-semibold ${mine ? 'bg-accent text-accent-ink' : 'bg-notice'}`}
          >
            <span>
              {message.type === 'ParticipantRemoved'
                ? `${data.name} was removed from the game`
                : mine
                  ? `🎉 You have Bingo!`
                  : `${data.name} had Bingo!`}
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
        <RulesNotice
          notice={game.rulesNotice}
          patterns={view.patterns}
          onClose={game.dismissRulesNotice}
        />
      )}

      {view.state === 'Starting' && pattern && (
        <section
          aria-live="polite"
          aria-label="This round"
          className="flex items-center gap-4 rounded-2xl border-2 border-accent bg-accent-soft p-4"
        >
          <PatternGrid
            mask={pattern.masks[0] ?? 0}
            columns={view.card.columns}
            rows={view.card.rows}
            freeCell={view.card.freeCell}
            earned={earned}
            label={`${pattern.name} pattern`}
          />
          <div>
            <p className="font-bold">This round: {pattern.name}</p>
            <p>Starts in {countdown}</p>
          </div>
        </section>
      )}

      {view.state === 'BetweenRounds' && (
        <p className="text-center text-ink-muted">Waiting for the host to start {roundName}.</p>
      )}

      {card && (
        <Card
          card={card}
          columns={view.card.columns}
          freeCell={view.card.freeCell}
          called={calls ? new Set(calls.map((c) => c.number)) : null}
          highlightMatches={view.settings.highlightMatches}
          markStyle={markStyle}
          setFaces={setFaces(view.board)}
          disabled={!inRound || marking.busy}
          onToggle={toggle}
        />
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
            className="min-h-16 w-full max-w-xs rounded-2xl bg-accent text-3xl font-black tracking-wide text-accent-ink focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50"
          >
            BINGO!
          </button>
          <p aria-live="polite" className="min-h-6 font-semibold">
            {sent ? 'Sent to the host!' : ''}
          </p>
          {bingo.error && <ErrorText>{bingo.error}</ErrorText>}
        </div>
      )}

      <div className="flex flex-wrap justify-center gap-2">
        {view.settings.showCallBoard && calls && (
          <Button
            look="secondary"
            aria-expanded={open === 'board'}
            onClick={() => {
              setOpen(open === 'board' ? undefined : 'board')
            }}
          >
            Call board
          </Button>
        )}
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

      <div className="flex flex-wrap justify-center gap-2">
        {inRound && card && (
          <Button
            look="secondary"
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
          >
            Undo
          </Button>
        )}
        <Button
          look="quiet"
          onClick={() => {
            setStyling(!styling)
          }}
        >
          ⚙ My marks
        </Button>
      </div>

      {styling && (
        <MarkStylePanel
          style={markStyle}
          onChange={setMarkStyle}
          onClose={() => {
            setStyling(false)
          }}
        />
      )}
    </div>
  )
}
