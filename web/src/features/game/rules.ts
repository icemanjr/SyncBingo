import type { Schemas } from '../../api/client'
import { selectionName, type PatternData } from './patterns'

export type Settings = Schemas['SettingsBody']
export type SettingKey = Exclude<keyof Settings, never>

interface Choice {
  value: string
  label: string
}

/** One host setting as Options shows it (FR-10 … FR-46). */
export interface SettingInfo {
  key: SettingKey
  label: string
  help: string
  /** Lobby only (card size, free space, redraw policy, multiple wins): locked during a round. */
  betweenRounds?: true
  choices?: Choice[]
}

/** Options' sections, in order. A setting without choices is a switch; `autoCallIntervalSeconds` is a number. */
export const settingSections: { title: string; settings: SettingInfo[] }[] = [
  {
    title: 'Calling',
    settings: [
      {
        key: 'autoCallIntervalSeconds',
        label: 'Seconds between balls in auto mode',
        help: '3 to 120.',
      },
    ],
  },
  {
    title: 'What players see',
    settings: [
      {
        key: 'showBallToPlayers',
        label: 'Show the called ball',
        help: 'The latest ball appears on phones.',
      },
      {
        key: 'highlightMatches',
        label: 'Highlight matching squares',
        help: 'A called number glows on the card.',
      },
      {
        key: 'showCallBoard',
        label: 'Show the call board',
        help: 'Players can open the board of called balls.',
      },
      {
        key: 'winHighlight',
        label: 'Celebrate wins',
        help: 'A celebration on screen when the host awards a win.',
      },
      {
        key: 'showRoundNumbers',
        label: 'Show round numbers',
        help: '"Round 3" rather than "This round".',
      },
    ],
  },
  {
    title: 'Marking',
    settings: [
      {
        key: 'freeMarking',
        label: 'Mark any square',
        help: 'Off: only called numbers can be marked.',
      },
      {
        key: 'unmarking',
        label: 'Removing marks',
        help: 'Undo always works, back to the last ball.',
        choices: [
          { value: 'Any', label: 'Any mark' },
          { value: 'MismarksOnly', label: 'Only marks on numbers not called' },
          { value: 'Off', label: 'Never' },
        ],
      },
      {
        key: 'autoFillAvailable',
        label: 'Offer auto-fill',
        help: 'Each player can switch it on for missed numbers.',
      },
      {
        key: 'requireMarksForClaims',
        label: 'Bingo needs marks',
        help: 'A claim counts only if the squares are marked.',
      },
    ],
  },
  {
    title: 'Alerts for you',
    settings: [
      {
        key: 'winDetection',
        label: 'Win detection',
        help: 'Alerts are only for you; you still decide every win.',
        choices: [
          { value: 'AlertHost', label: 'Alert me' },
          { value: 'AlertHostAndPause', label: 'Alert me and pause' },
          { value: 'Off', label: 'Off' },
        ],
      },
    ],
  },
  {
    title: 'Between rounds',
    settings: [
      {
        key: 'cardSize',
        label: 'Card size',
        help: '15 balls per column: 3 × 3 is B I N (1–45), 4 × 4 is B I N G (1–60). Changing it deals everyone a new card.',
        betweenRounds: true,
        choices: [
          { value: 'FiveByFive', label: '5 × 5' },
          { value: 'FourByFour', label: '4 × 4' },
          { value: 'ThreeByThree', label: '3 × 3' },
        ],
      },
      {
        key: 'freeSpace',
        label: 'Free space',
        help: 'Earned: marked once a called ball is not on the card. None: the centre is a square like any other, and every card is redealt.',
        betweenRounds: true,
        choices: [
          { value: 'AlwaysMarked', label: 'Always marked' },
          { value: 'Earned', label: 'Earned' },
          { value: 'None', label: 'None' },
        ],
      },
      {
        key: 'redrawPolicy',
        label: 'New cards',
        help: 'Who may swap a card between rounds.',
        betweenRounds: true,
        choices: [
          { value: 'HostControls', label: 'Only you' },
          { value: 'ParticipantsMayRedraw', label: 'Players may swap' },
        ],
      },
      {
        key: 'multipleWins',
        label: 'Multiple wins',
        help: 'How often one card can be awarded in a round.',
        betweenRounds: true,
        choices: [
          { value: 'OncePerPattern', label: 'Once per pattern' },
          { value: 'OncePerRound', label: 'Once per round' },
          { value: 'EveryDeclaredWin', label: 'Every declared win' },
        ],
      },
    ],
  },
]

// Each sentence states the rule as it now stands, not the change ("not highlighted", never "no longer"),
// because the same words list the current rules (FR-48) as well as announce changes (FR-47).
const sentences: Record<string, (value: string) => string> = {
  showBallToPlayers: (v) =>
    v === 'true'
      ? 'The called ball is shown on your screen'
      : 'The called ball is not shown on your screen',
  highlightMatches: (v) =>
    v === 'true'
      ? 'Called numbers are highlighted on your card'
      : 'Called numbers are not highlighted',
  showCallBoard: (v) => (v === 'true' ? 'You can open the call board' : 'The call board is hidden'),
  autoFillAvailable: (v) => (v === 'true' ? 'Auto-fill is available' : 'Auto-fill is turned off'),
  freeMarking: (v) =>
    v === 'true' ? 'Any square can be marked' : 'Only called numbers can be marked',
  requireMarksForClaims: (v) =>
    v === 'true' ? 'Bingo needs your squares marked' : 'Can call Bingo without marks',
  unmarking: (v) =>
    v === 'Any'
      ? 'Any mark can be removed'
      : v === 'MismarksOnly'
        ? 'Only marks on numbers not called can be removed'
        : "Marks can't be removed (Undo still works)",
  freeSpace: (v) =>
    v === 'Earned'
      ? 'The free space must be earned'
      : v === 'None'
        ? 'No free space: every square is a ball'
        : 'The free space is always marked',
  redrawPolicy: (v) =>
    v === 'ParticipantsMayRedraw'
      ? 'You may swap your card between rounds'
      : 'Only the host swaps cards',
  multipleWins: (v) =>
    v === 'EveryDeclaredWin'
      ? 'Every line you declare can win'
      : v === 'OncePerRound'
        ? 'One win per card per round'
        : 'One win per card per pattern',
}

/** A rules change in plain words (FR-47), e.g. "Pattern changed to Normal Bingo or Four Corners". */
export function describeChange(change: { rule: string; value: string }, data: PatternData): string {
  if (change.rule === 'winCondition')
    return `Pattern changed to ${selectionName(change.value, data)}`

  return sentences[change.rule]?.(change.value) ?? `${change.rule}: ${change.value}`
}
