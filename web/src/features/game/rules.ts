import type { Schemas } from '../../api/client'

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
  /** Lobby only (free space, redraw policy, multiple wins): locked during a round. */
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
        key: 'freeSpace',
        label: 'Free space',
        help: 'Earned: marked once a called ball is not on the card.',
        betweenRounds: true,
        choices: [
          { value: 'AlwaysMarked', label: 'Always marked' },
          { value: 'Earned', label: 'Earned' },
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

const sentences: Record<string, (value: string) => string> = {
  showBallToPlayers: (v) =>
    v === 'true' ? 'The called ball is shown on your screen' : 'The called ball is no longer shown',
  highlightMatches: (v) =>
    v === 'true'
      ? 'Called numbers are highlighted on your card'
      : 'Called numbers are no longer highlighted',
  showCallBoard: (v) => (v === 'true' ? 'You can open the call board' : 'The call board is hidden'),
  autoFillAvailable: (v) => (v === 'true' ? 'Auto-fill is available' : 'Auto-fill is turned off'),
  freeMarking: (v) =>
    v === 'true' ? 'Any square can be marked' : 'Only called numbers can be marked',
  requireMarksForClaims: (v) =>
    v === 'true' ? 'Bingo needs your squares marked' : 'Bingo no longer needs marks',
  unmarking: (v) =>
    v === 'Any'
      ? 'Any mark can be removed'
      : v === 'MismarksOnly'
        ? 'Only marks on numbers not called can be removed'
        : "Marks can't be removed (Undo still works)",
  freeSpace: (v) =>
    v === 'Earned' ? 'The free space must be earned' : 'The free space is always marked',
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

/** A rules change in plain words (FR-47), e.g. "Pattern changed to Blackout". */
export function describeChange(
  change: { rule: string; value: string },
  patterns: Schemas['PatternView'][],
): string {
  if (change.rule === 'winCondition') {
    const name = patterns.find((p) => p.id === change.value)?.name ?? change.value
    return `Pattern changed to ${name}`
  }

  return sentences[change.rule]?.(change.value) ?? `${change.rule}: ${change.value}`
}
