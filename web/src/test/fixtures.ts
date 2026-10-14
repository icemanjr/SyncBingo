import type { Schemas } from '../api/client'
import type { Snapshot } from '../features/game/gameEvents'

const letters = ['B', 'I', 'N', 'G', 'O']

/** A standard 75-ball game in the lobby, as the API's snapshot describes it; override what a test needs. */
export function aGame(changes: Partial<Schemas['GameView']> = {}): Schemas['GameView'] {
  return {
    code: '9ve4-shp3-k7mw-a2qx',
    state: 'BetweenRounds',
    round: 0,
    upcomingRound: 1,
    countdownEndsAt: null,
    winCondition: 'normal',
    patterns: [
      { id: 'normal', name: 'Normal Bingo', masks: [31] },
      { id: 'blackout', name: 'Blackout', masks: [33554431] },
    ],
    card: { rows: 5, columns: 5, freeCell: 12 },
    board: letters.map((label, i) => ({
      label,
      balls: Array.from({ length: 15 }, (_, n) => i * 15 + n + 1),
    })),
    calls: [],
    ballsRemaining: 75,
    nextBallFrom: null,
    settings: {
      showBallToPlayers: true,
      highlightMatches: false,
      showCallBoard: true,
      autoFillAvailable: false,
      winHighlight: true,
      freeMarking: true,
      requireMarksForClaims: false,
      unmarking: 'Any',
      freeSpace: 'AlwaysMarked',
      redrawPolicy: 'HostControls',
      multipleWins: 'OncePerPattern',
      showRoundNumbers: true,
    },
    leaderboard: [],
    version: 1,
    seq: 0,
    joinNote: null,
    leaderboardVisible: false,
    ...changes,
  }
}

export function aHostSnapshot(
  game: Partial<Schemas['GameView']> = {},
  host: Partial<Schemas['HostBody']> = {},
): Snapshot {
  return {
    game: aGame(game),
    you: null,
    host: {
      settings: { multipleWins: 'OncePerPattern', autoCallIntervalSeconds: 10 },
      participants: [],
      pendingClaims: [],
      rounds: [],
      ...host,
    },
  }
}

export const aCall = (number: number, sequence = 1): Schemas['BallCall'] => ({
  sequence,
  number,
  letter: letters[Math.floor((number - 1) / 15)] ?? '?',
  calledAt: '2026-10-14T19:00:00Z',
  source: 'Manual',
})
