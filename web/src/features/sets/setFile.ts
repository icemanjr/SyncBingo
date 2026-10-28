import { newId, squareOf, ungroupedOf, type Draft, type DraftEntry, type DraftGroup } from './draft'
import { defaultLabel, limits, tidy } from './text'

/**
 * The set file (ADR 0009) in the browser: read an uploaded file into a draft, and write a draft in the same
 * layout the server writes (`ContentSetFile`), so a file downloaded from either reads the same.
 */
export const fileFormat = 1

const notASetFile =
  'This is not a SyncBingo set file. Download one from the set editor to see the format.'

/** A file's groups and pairs as a draft; or why it can't be read. The set's own rules are checked later. */
export function readSetFile(text: string): { draft: Draft } | { error: string } {
  if (new TextEncoder().encode(text).length > limits.fileBytes)
    return { error: `A set file can be at most ${String(limits.fileBytes / 1024)} KB.` }

  let file: unknown
  try {
    file = JSON.parse(text)
  } catch {
    return { error: notASetFile }
  }

  if (typeof file !== 'object' || file === null || Array.isArray(file))
    return { error: notASetFile }
  const { format, name, free, groups, pairs } = file as Record<string, unknown>
  if (typeof format !== 'number' || !Number.isInteger(format) || format < 1)
    return { error: notASetFile }
  if (format > fileFormat)
    return {
      error:
        'This set file was made by a newer version of SyncBingo; reload the page and try again.',
    }
  if (name !== undefined && typeof name !== 'string') return { error: notASetFile }
  if (free !== undefined && typeof free !== 'string') return { error: notASetFile }
  if (groups === undefined && pairs === undefined) return { error: notASetFile }

  const draft: Draft = { name: tidy(name ?? ''), free: tidy(free ?? ''), groups: [], entries: [] }
  const readPairs = (list: unknown, groupId: string | null): boolean => {
    if (!Array.isArray(list)) return false
    for (const pair of list) {
      if (
        !Array.isArray(pair) ||
        pair.length < 1 ||
        pair.length > 2 ||
        pair.some((side) => typeof side !== 'string')
      )
        return false
      const [ball, square] = pair as string[]
      const entry: DraftEntry = {
        id: newId('e'),
        ball: tidy(ball),
        square: square === undefined ? null : tidy(square),
        groupId,
      }
      if (entry.square === entry.ball || entry.square === '') entry.square = null
      draft.entries.push(entry)
    }
    return true
  }

  if (groups !== undefined) {
    if (!Array.isArray(groups)) return { error: notASetFile }
    for (const group of groups) {
      if (typeof group !== 'object' || group === null) return { error: notASetFile }
      const g = group as Record<string, unknown>
      if (typeof g.name !== 'string' || (g.label !== undefined && typeof g.label !== 'string'))
        return { error: notASetFile }
      const groupName = tidy(g.name)
      const read: DraftGroup = {
        id: newId('g'),
        name: groupName,
        label: tidy(g.label) || defaultLabel(groupName),
      }
      draft.groups.push(read)
      if (!readPairs(g.pairs, read.id)) return { error: notASetFile }
    }
  }

  if (pairs !== undefined && !readPairs(pairs, null)) return { error: notASetFile }
  return { draft }
}

const quote = (text: string) => JSON.stringify(text)

/** One pair to a line: `["cat"]` or `["2 + 5", "7"]`. */
const pairsBlock = (entries: DraftEntry[], indent: string): string =>
  entries.length === 0
    ? '[]'
    : `[\n${entries
        .map((entry) =>
          entry.square === null || entry.square === entry.ball
            ? `${indent}  [${quote(entry.ball)}]`
            : `${indent}  [${quote(entry.ball)}, ${quote(squareOf(entry))}]`,
        )
        .join(',\n')}\n${indent}]`

/**
 * The draft as its file, in the server's layout: groups in order, each with its pairs, then the ungrouped ones.
 * Without `useGroups` the file has no groups, so the set is dealt free-form (the draft keeps them, hidden).
 */
export function writeSetFile(draft: Draft, useGroups = true): string {
  const lines = [
    `{\n  "format": ${String(fileFormat)},\n  "name": ${quote(tidy(draft.name) || 'Custom set')}`,
  ]
  if (tidy(draft.free)) lines.push(`,\n  "free": ${quote(tidy(draft.free))}`)
  if (!useGroups) {
    lines.push(`,\n  "pairs": ${pairsBlock(draft.entries, '  ')}`)
    return `${lines.join('')}\n}\n`
  }

  if (draft.groups.length > 0) {
    const groups = draft.groups.map(
      (group) =>
        `    {\n      "name": ${quote(group.name)},\n      "label": ${quote(group.label || defaultLabel(group.name))},\n      "pairs": ${pairsBlock(
          draft.entries.filter((entry) => entry.groupId === group.id),
          '      ',
        )}\n    }`,
    )
    lines.push(`,\n  "groups": [\n${groups.join(',\n')}\n  ]`)
  }

  const ungrouped = ungroupedOf(draft)
  if (ungrouped.length > 0) lines.push(`,\n  "pairs": ${pairsBlock(ungrouped, '  ')}`)
  return `${lines.join('')}\n}\n`
}
