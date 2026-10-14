import type { Schemas } from '../../api/client'
import type { GameEvent } from '../../realtime/gameConnection'

export type Snapshot = Schemas['SnapshotBody']
export type Game = Schemas['GameView']
export type BallCall = Schemas['BallCall']

/** A "Rules updated" notice to show (FR-47). */
export interface RulesNotice {
  changes: { rule: string; value: string }[]
  nextBallFrom: string
}

export interface Applied {
  snapshot: Snapshot
  /** Fetch the snapshot again: the event changed something only the snapshot shows in full (players, claims). */
  refetch: boolean
  rulesNotice?: RulesNotice
  /** A win or detection the host should see now, before the refreshed snapshot arrives. */
  alert?: { type: string; data: unknown }
}

/** The fields events carry (docs/05 §4); each event uses a few of them. */
interface EventData {
  call?: BallCall
  endsAt?: string
  winConditionId?: string
  round?: number
  nextBallFrom?: string
  note?: string | null
  visible?: boolean
}

const totalBalls = (game: Game) => game.board.reduce((sum, group) => sum + group.balls.length, 0)

/**
 * Applies a session event (docs/05 §4) to the snapshot the screen shows. Pure, so every screen (host,
 * player, display) shares it and it is tested on its own. Events the snapshot can't absorb on the spot
 * (claims, wins, removals) ask for a refetch instead.
 */
export function applyEvent(snapshot: Snapshot, event: GameEvent): Applied {
  const game = snapshot.game
  const data = (event.data ?? {}) as EventData
  const with_ = (changes: Partial<Game>): Applied => ({
    snapshot: { ...snapshot, game: { ...game, ...changes } },
    refetch: false,
  })

  switch (event.type) {
    case 'BallCalled': {
      const call = data.call
      if (!call) return { snapshot, refetch: true }
      return with_({
        calls: game.calls ? [...game.calls, call] : null,
        ballsRemaining: game.ballsRemaining - 1,
        nextBallFrom: null,
      })
    }
    case 'RoundCountdownStarted':
      return with_({
        state: 'Starting',
        countdownEndsAt: data.endsAt ?? null,
        winCondition: data.winConditionId ?? game.winCondition,
      })
    case 'RoundCountdownCancelled':
      return with_({ state: 'BetweenRounds', countdownEndsAt: null })
    case 'RoundStarted':
      return with_({
        state: 'Manual',
        round: data.round ?? game.upcomingRound,
        upcomingRound: data.round ?? game.upcomingRound,
        countdownEndsAt: null,
        calls: game.calls ? [] : null,
        ballsRemaining: totalBalls(game),
        nextBallFrom: null,
      })
    case 'AutoCallingStarted':
      return with_({ state: 'Auto' })
    case 'AutoCallingPaused':
      return with_({ state: 'Manual' })
    case 'RoundEnded':
      return with_({ state: 'BetweenRounds', upcomingRound: game.round + 1 })
    case 'CardsCleared':
      return {
        ...with_({
          state: 'BetweenRounds',
          upcomingRound: game.round + 1,
          calls: game.calls ? [] : null,
          ballsRemaining: totalBalls(game),
          nextBallFrom: null,
        }),
        refetch: true, // cards and marks change too
      }
    case 'WinConditionChanged':
      return with_({ winCondition: data.winConditionId ?? game.winCondition })
    case 'RulesUpdated':
      return {
        ...with_({ nextBallFrom: data.nextBallFrom ?? null }),
        rulesNotice: data as unknown as RulesNotice,
      }
    case 'JoinNoteChanged':
      return with_({ joinNote: data.note ?? null })
    case 'SessionEnded':
      return with_({ state: 'Ended' })
    case 'LeaderboardVisibilityChanged':
      return {
        ...with_({ leaderboardVisible: data.visible ?? game.leaderboardVisible }),
        refetch: true,
      } // players now get the entries
    case 'SettingsChanged':
    case 'WinClaimed':
    case 'ClaimDismissed':
    case 'ParticipantRemoved':
      return { snapshot, refetch: true }
    case 'WinAwarded':
    case 'WinDetected':
      return { snapshot, refetch: true, alert: { type: event.type, data: event.data } }
    default:
      return { snapshot, refetch: true } // unknown to this client version: the snapshot knows best
  }
}
