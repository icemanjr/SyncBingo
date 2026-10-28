import type { Schemas } from '../api/client'
import type { Snapshot } from '../features/game/gameEvents'

const letters = ['B', 'I', 'N', 'G', 'O']

const line = (cells: number[]) => cells.reduce((mask, cell) => mask | (1 << cell), 0)

/** The patterns standard Bingo offers (as the server sends them), Normal Bingo being rows, columns and diagonals. */
export const standardPatterns: Schemas['PatternView'][] = [
  {
    id: 'rows',
    name: 'Rows',
    masks: [0, 1, 2, 3, 4].map((r) => line([0, 1, 2, 3, 4].map((c) => r * 5 + c))),
    isLine: true,
    announcedAs: 'Rows',
  },
  {
    id: 'columns',
    name: 'Columns',
    masks: [0, 1, 2, 3, 4].map((c) => line([0, 1, 2, 3, 4].map((r) => r * 5 + c))),
    isLine: true,
    announcedAs: 'Columns',
  },
  {
    id: 'diagonals',
    name: 'Diagonals',
    masks: [line([0, 6, 12, 18, 24]), line([4, 8, 12, 16, 20])],
    isLine: true,
    announcedAs: 'Diagonals',
  },
  {
    id: 'corners',
    name: 'Four Corners',
    masks: [line([0, 4, 20, 24])],
    isLine: false,
    announcedAs: 'Four Corners',
  },
  { id: 'B', name: 'Letter B', masks: [1], isLine: false, announcedAs: 'the letter B' },
  { id: 'blackout', name: 'Blackout', masks: [33554431], isLine: false, announcedAs: 'Blackout' },
]

/** A standard 75-ball game in the lobby, as the API's snapshot describes it; override what a test needs. */
export function aGame(changes: Partial<Schemas['GameView']> = {}): Schemas['GameView'] {
  return {
    code: '9ve4-shp3-k7mw-a2qx',
    state: 'BetweenRounds',
    round: 0,
    upcomingRound: 1,
    countdownEndsAt: null,
    winCondition: 'rows+columns+diagonals',
    patterns: standardPatterns,
    presets: [{ id: 'normal', name: 'Normal Bingo', patternIds: ['rows', 'columns', 'diagonals'] }],
    card: { rows: 5, columns: 5, freeCell: 12 },
    freeText: 'FREE',
    cardSizes: [
      { size: 'FiveByFive', rows: 5, columns: 5, patterns: standardPatterns },
      // Smaller cards have no letters (FR-26a); their masks don't matter to these tests.
      {
        size: 'FourByFour',
        rows: 4,
        columns: 4,
        patterns: standardPatterns.filter((p) => p.id !== 'B'),
      },
      {
        size: 'ThreeByThree',
        rows: 3,
        columns: 3,
        patterns: standardPatterns.filter((p) => p.id !== 'B'),
      },
    ],
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

/** A player's snapshot: the game as a player sees it, and their own card (cells 1–24 around the free center). */
export function aPlayerSnapshot(
  game: Partial<Schemas['GameView']> = {},
  marks = 1 << 12,
): Snapshot {
  const cells = Array.from({ length: 25 }, (_, cell) =>
    cell === 12 ? 0 : cell < 12 ? cell + 1 : cell,
  )
  return {
    game: aGame({ joinNote: null, ...game }),
    you: {
      participantId: 'p1',
      name: 'Ava',
      autoFillOn: false,
      cards: [{ cardId: 1, cells, marks }],
      waiting: false,
    },
    host: null,
  }
}
