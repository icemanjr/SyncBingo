import type { Schemas } from '../../api/client'
import { selectionName, type PatternData } from '../game/patterns'

type Round = Schemas['RoundSummary']
type Win = Schemas['WinRecord']

/**
 * The game summary (FR-40a): what the host keeps after ending a game. Built in the host's browser from the
 * leaderboard and round summaries the server already has, and kept only on that device (sessionStorage),
 * since the server deletes the game minutes after it ends. Wins only, never dismissed Bingo presses.
 */
export interface GameSummary {
  code: string
  endedAt: string
  players: number
  leaderboard: Schemas['LeaderboardEntry'][]
  rounds: Round[]
  /** Every pattern the game had, on every card size, to name the rounds' patterns. */
  patterns: PatternData
}

const key = (code: string) => `syncbingo:summary:${code}`

export const summaries = {
  save(summary: GameSummary): void {
    try {
      sessionStorage.setItem(key(summary.code), JSON.stringify(summary))
    } catch {
      // Not saved: the summary page still shows it until this tab moves on.
    }
  },
  load(code: string): GameSummary | undefined {
    try {
      const saved = sessionStorage.getItem(key(code))
      return saved ? (JSON.parse(saved) as GameSummary) : undefined
    } catch {
      return undefined
    }
  },
}

/** The game's patterns on every card size, so a round played on another size is still named. */
export function allPatterns(game: Schemas['GameView']): PatternData {
  const byId = new Map(game.patterns.map((pattern) => [pattern.id, pattern]))
  for (const size of game.cardSizes)
    for (const pattern of size.patterns) if (!byId.has(pattern.id)) byId.set(pattern.id, pattern)
  return { patterns: [...byId.values()], presets: game.presets }
}

/** The totals at the top of the summary. */
export function totals(summary: GameSummary) {
  return {
    players: summary.players,
    rounds: summary.rounds.length,
    balls: summary.rounds.reduce((sum, round) => sum + round.ballsCalled, 0),
    wins: summary.rounds.reduce(
      (sum, round) => sum + round.wins.reduce((n, win) => n + win.wins, 0),
      0,
    ),
  }
}

/** A round's patterns by name: "Normal Bingo", then "Blackout" if the host changed it mid-round. */
export const roundPatterns = (round: Round, patterns: PatternData): string =>
  round.winConditionsUsed.map((key) => selectionName(key, patterns)).join(', then ')

/** What a win was announced as: a shape's name ("Four Corners"), else "Bingo". */
export const wonWith = (win: Win, patterns: PatternData): string =>
  patterns.patterns.find((pattern) => pattern.id === win.announced)?.name ?? 'Bingo'

/** How a win came about (FR-37a, FR-19, FR-38). */
export const howWon = (win: Win): string =>
  win.source === 'Claimed'
    ? 'Pressed Bingo'
    : win.source === 'Detected'
      ? 'Detected by SyncBingo'
      : 'Awarded by the host'

/** A time as people read it on this device, e.g. "7:05 PM". */
export const timeOf = (iso: string | null | undefined): string =>
  iso ? new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }) : ''

/** Whole minutes from start to end, or undefined while a round has no end. */
export function minutesOf(round: Round): number | undefined {
  if (!round.endedAt) return undefined
  return Math.max(0, Math.round((Date.parse(round.endedAt) - Date.parse(round.startedAt)) / 60000))
}

/**
 * One CSV field. A value a spreadsheet would run as a formula (starting with = + - @, a tab or a carriage
 * return) gets a leading apostrophe, so a player's name can never run as one (docs/02 §7a); a value with a
 * comma, quote or line break is quoted.
 */
export function csvField(value: string | number): string {
  let text = String(value)
  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

/** The summary as a spreadsheet: one row per win; a round nobody won is one row with no winner. */
export function summaryCsv(summary: GameSummary): string {
  const header = [
    'Round',
    'Started',
    'Ended',
    'Balls called',
    'Patterns',
    'Set',
    'Winner',
    'Won with',
    'How',
    'Wins',
  ]
  const rows: (string | number)[][] = []
  for (const round of summary.rounds) {
    const base = [
      round.number,
      round.startedAt,
      round.endedAt ?? '',
      round.ballsCalled,
      roundPatterns(round, summary.patterns),
      round.contentName ?? 'Numbers',
    ]
    if (round.wins.length === 0) rows.push([...base, '', '', '', ''])
    for (const win of round.wins)
      rows.push([...base, win.name, wonWith(win, summary.patterns), howWon(win), win.wins])
  }
  return `${[header, ...rows].map((row) => row.map(csvField).join(',')).join('\r\n')}\r\n`
}
