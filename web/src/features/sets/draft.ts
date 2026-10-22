import { defaultLabel, fits, isPrintable, key, limits, tidy } from './text'

/**
 * A custom set while the host edits it (ADR 0009): kept on their device and sent whole with **Use this set**,
 * so players get one redeal, not one per edit. Ids are the editor's own, for React keys and moves.
 */
export interface Draft {
  name: string
  groups: DraftGroup[]
  entries: DraftEntry[]
}

export interface DraftGroup {
  id: string
  name: string
  /** Up to 5 characters above the group's card column; pre-filled with the name's first letter. */
  label: string
}

export interface DraftEntry {
  id: string
  /** What is called. */
  ball: string
  /** What players mark; `null` when it is the same as the ball (Ball and square match). */
  square: string | null
  groupId: string | null
}

let next = 0
/** A new id for an entry or group, unique within this page. */
export const newId = (prefix: string): string => `${prefix}${String(++next)}`

export const emptyDraft = (): Draft => ({ name: '', groups: [], entries: [] })

/** What a square shows: its own text, or the ball's when they match. */
export const squareOf = (entry: DraftEntry): string => entry.square ?? entry.ball

/** Why an entry can't be added as it is, or `undefined` when it can. */
export type EntryProblem =
  | { kind: 'empty'; side: 'ball' | 'square' }
  | { kind: 'tooLong'; side: 'ball' | 'square'; max: number }
  | { kind: 'unprintable'; side: 'ball' | 'square' }
  | { kind: 'duplicate' }
  | { kind: 'clash'; side: 'ball' | 'square'; with: DraftEntry }

/**
 * Checks one entry against the set's rules and the other entries (all but `except`, the entry being edited):
 * each side within its limit and printable, and neither side already used by another entry.
 */
export function checkEntry(
  draft: Draft,
  ball: string,
  square: string | null,
  except?: string,
): EntryProblem | undefined {
  const b = tidy(ball)
  const s = square === null ? b : tidy(square) || b
  if (!b) return { kind: 'empty', side: 'ball' }
  if (!fits(b, limits.ball)) return { kind: 'tooLong', side: 'ball', max: limits.ball }
  if (!isPrintable(b)) return { kind: 'unprintable', side: 'ball' }
  if (!fits(s, limits.square)) return { kind: 'tooLong', side: 'square', max: limits.square }
  if (!isPrintable(s)) return { kind: 'unprintable', side: 'square' }

  for (const other of draft.entries) {
    if (other.id === except) continue
    const sameBall = key(tidy(other.ball)) === key(b)
    const sameSquare = key(tidy(squareOf(other))) === key(s)
    if (sameBall && sameSquare) return { kind: 'duplicate' }
    if (sameBall) return { kind: 'clash', side: 'ball', with: other }
    if (sameSquare) return { kind: 'clash', side: 'square', with: other }
  }

  return undefined
}

/** A problem as a sentence for the host. */
export function describe(problem: EntryProblem): string {
  switch (problem.kind) {
    case 'empty':
      return problem.side === 'ball' ? 'Type what is called.' : 'Type what players mark.'
    case 'tooLong':
      return problem.side === 'ball'
        ? `What is called can be up to ${String(problem.max)} characters.`
        : `A square can be up to ${String(problem.max)} characters.`
    case 'unprintable':
      return 'That has characters that can’t be shown.'
    case 'duplicate':
      return 'That entry is already in the set.'
    case 'clash':
      return problem.side === 'ball'
        ? `“${tidy(problem.with.ball)}” is already called for “${squareOf(problem.with)}”.`
        : `“${squareOf(problem.with)}” is already a square, for “${tidy(problem.with.ball)}”.`
  }
}

/**
 * Adds an entry (tidied), into `groupId` or ungrouped; refuses it with the problem instead if it breaks a
 * rule (`checkEntry`). With `matched`, the square is the ball's text.
 */
export function addEntry(
  draft: Draft,
  ball: string,
  square: string | null,
  groupId: string | null,
): { draft: Draft; problem?: EntryProblem } {
  const problem = checkEntry(draft, ball, square)
  if (problem) return { draft, problem }
  const b = tidy(ball)
  const s = square === null ? null : tidy(square) || null
  const entry: DraftEntry = { id: newId('e'), ball: b, square: s === b ? null : s, groupId }
  return { draft: { ...draft, entries: [...draft.entries, entry] } }
}

/** Edits an entry in place, with the same checks as adding it (against every other entry). */
export function editEntry(
  draft: Draft,
  id: string,
  ball: string,
  square: string | null,
): { draft: Draft; problem?: EntryProblem } {
  const problem = checkEntry(draft, ball, square, id)
  if (problem) return { draft, problem }
  const b = tidy(ball)
  const s = square === null ? null : tidy(square) || null
  return {
    draft: {
      ...draft,
      entries: draft.entries.map((entry) =>
        entry.id === id ? { ...entry, ball: b, square: s === b ? null : s } : entry,
      ),
    },
  }
}

export const deleteEntry = (draft: Draft, id: string): Draft => ({
  ...draft,
  entries: draft.entries.filter((entry) => entry.id !== id),
})

/** Moves an entry into another group, or out of every group (`null`). */
export const moveEntry = (draft: Draft, id: string, groupId: string | null): Draft => ({
  ...draft,
  entries: draft.entries.map((entry) => (entry.id === id ? { ...entry, groupId } : entry)),
})

/** Why a group name can't be used, or `undefined`. */
export function checkGroupName(draft: Draft, name: string, except?: string): string | undefined {
  const n = tidy(name)
  if (!n) return 'Give the group a name.'
  if (!fits(n, limits.groupName) || !isPrintable(n))
    return `A group name can be up to ${String(limits.groupName)} characters.`
  if (draft.groups.some((group) => group.id !== except && key(group.name) === key(n)))
    return `There is already a group called “${n}”.`
  return undefined
}

/**
 * Renames a group: its entries follow, since they point at the group, not its name. A label the host never
 * changed (still the old name's first letter) follows the new name's first letter.
 */
export function renameGroup(
  draft: Draft,
  id: string,
  name: string,
): { draft: Draft; problem?: string } {
  const problem = checkGroupName(draft, name, id)
  if (problem) return { draft, problem }
  const n = tidy(name)
  return {
    draft: {
      ...draft,
      groups: draft.groups.map((group) =>
        group.id === id
          ? {
              ...group,
              name: n,
              label: group.label === defaultLabel(group.name) ? defaultLabel(n) : group.label,
            }
          : group,
      ),
    },
  }
}

/** Sets a group's short label; empty means the default (its first letter). */
export function setLabel(
  draft: Draft,
  id: string,
  label: string,
): { draft: Draft; problem?: string } {
  const l = tidy(label)
  if (l && (!fits(l, limits.label) || !isPrintable(l)))
    return { draft, problem: `A label can be up to ${String(limits.label)} characters.` }
  return {
    draft: {
      ...draft,
      groups: draft.groups.map((group) =>
        group.id === id ? { ...group, label: l || defaultLabel(group.name) } : group,
      ),
    },
  }
}

/** Moves a group one place earlier (-1) or later (+1): the order the card's columns are dealt in. */
export function moveGroup(draft: Draft, id: string, by: -1 | 1): Draft {
  const from = draft.groups.findIndex((group) => group.id === id)
  const to = from + by
  if (from < 0 || to < 0 || to >= draft.groups.length) return draft
  const groups = [...draft.groups]
  const [moved] = groups.splice(from, 1)
  if (moved) groups.splice(to, 0, moved)
  return { ...draft, groups }
}

/** Column letters, left to right, named for the groups a card gains (as in B-I-N-G-O). */
const columnLetters = ['B', 'I', 'N', 'G', 'O']

/**
 * Fits the groups to the card's columns (ADR 0009), for **Use groups**: one group per column, never more.
 * Groups past the last column are removed and their entries become ungrouped pairs, for the host to move or
 * remove; missing columns are added at the end as empty groups, named by their column letter ("G", "O").
 * Nothing is deleted here, so the set can't be used until the host has made it fit (`setProblems`).
 */
export function fitGroups(draft: Draft, columns: number): Draft {
  const groups = draft.groups.slice(0, columns)
  const kept = new Set(groups.map((group) => group.id))
  for (let column = groups.length; column < columns; column++) {
    const taken = (name: string) => groups.some((group) => key(group.name) === key(name))
    let name = columnLetters[column] ?? `Column ${String(column + 1)}`
    for (let n = 2; taken(name); n++) name = `Column ${String(column + 1)} (${String(n)})`
    groups.push({ id: newId('g'), name, label: defaultLabel(name) })
  }

  return {
    ...draft,
    groups,
    entries: draft.entries.map((entry) =>
      entry.groupId !== null && !kept.has(entry.groupId) ? { ...entry, groupId: null } : entry,
    ),
  }
}

/** The entries in no group (or in one no longer in the draft). */
export const ungroupedOf = (draft: Draft): DraftEntry[] =>
  draft.entries.filter(
    (entry) => entry.groupId === null || !draft.groups.some((group) => group.id === entry.groupId),
  )

/** **Remove all** ungrouped pairs, e.g. to trim a set to a smaller card, as standard Bingo drops columns. */
export function removeUngrouped(draft: Draft): Draft {
  const ungrouped = new Set(ungroupedOf(draft).map((entry) => entry.id))
  return { ...draft, entries: draft.entries.filter((entry) => !ungrouped.has(entry.id)) }
}

/** What a merged file added and what it skipped (FR-44): skipped entries are reported, never overwritten. */
export interface MergeReport {
  added: number
  duplicates: number
  clashes: { ball: string; square: string }[]
}

/**
 * **Add from file** (FR-44): merges another set into this one, e.g. week 2's new words into week 1's list.
 * Groups of the same name (ignoring case) combine; new groups are added after the existing ones. Entries
 * already in the set are skipped as duplicates; ones that reuse a ball or a square with another partner are
 * skipped as clashes. The name stays this draft's, unless it has none.
 */
export function mergeDraft(draft: Draft, incoming: Draft): { draft: Draft; report: MergeReport } {
  let merged: Draft = { ...draft, name: draft.name || incoming.name }
  const groupFor = new Map<string, string>()
  for (const group of incoming.groups) {
    const existing = merged.groups.find((g) => key(g.name) === key(group.name))
    if (existing) {
      groupFor.set(group.id, existing.id)
    } else {
      const added: DraftGroup = { ...group, id: newId('g') }
      merged = { ...merged, groups: [...merged.groups, added] }
      groupFor.set(group.id, added.id)
    }
  }

  const report: MergeReport = { added: 0, duplicates: 0, clashes: [] }
  for (const entry of incoming.entries) {
    const result = addEntry(
      merged,
      entry.ball,
      entry.square,
      entry.groupId ? (groupFor.get(entry.groupId) ?? null) : null,
    )
    if (!result.problem) {
      merged = result.draft
      report.added++
    } else if (result.problem.kind === 'duplicate') {
      report.duplicates++
    } else {
      report.clashes.push({ ball: entry.ball, square: squareOf(entry) })
    }
  }

  return { draft: merged, report }
}

/** The report as one sentence: "Added 12. 3 duplicates skipped; 2 clash with existing entries: …". */
export function describeMerge(report: MergeReport): string {
  const parts = [`Added ${String(report.added)}.`]
  const skipped: string[] = []
  if (report.duplicates > 0)
    skipped.push(
      `${String(report.duplicates)} duplicate${report.duplicates === 1 ? '' : 's'} skipped`,
    )
  if (report.clashes.length > 0)
    skipped.push(
      `${String(report.clashes.length)} ${report.clashes.length === 1 ? 'clashes' : 'clash'} with existing entries: ${report.clashes
        .map((clash) =>
          clash.ball === clash.square ? `“${clash.ball}”` : `“${clash.ball}” / “${clash.square}”`,
        )
        .join(', ')}`,
    )
  if (skipped.length > 0) parts.push(`${skipped.join('; ')}.`)
  return parts.join(' ')
}

/**
 * What stops the draft being used, as the server would say, or nothing when it can be sent. With **Use groups**
 * (after `fitGroups`), every entry must be in a group and each group must fill its column.
 */
export function setProblems(
  draft: Draft,
  useGroups: boolean,
  cardColumns = 5,
  freeColumn: number | null = 2,
  rows = 5,
): string[] {
  const problems: string[] = []
  const count = draft.entries.length
  if (count < limits.minEntries || count > limits.maxEntries)
    problems.push(
      `A set needs ${String(limits.minEntries)}–${String(limits.maxEntries)} entries; this one has ${String(count)}.`,
    )
  else if (count < rows * cardColumns - (freeColumn === null ? 0 : 1))
    problems.push(
      `This card needs at least ${String(rows * cardColumns - (freeColumn === null ? 0 : 1))} entries; the set has ${String(count)}.`,
    )
  if (!fits(tidy(draft.name), limits.name))
    problems.push(`The set’s name can be up to ${String(limits.name)} characters.`)
  if (useGroups) {
    const ungrouped = ungroupedOf(draft).length
    if (ungrouped > 0)
      problems.push(
        `${String(ungrouped)} ungrouped ${ungrouped === 1 ? 'pair' : 'pairs'}: move ${ungrouped === 1 ? 'it' : 'them'} into a group, or remove ${ungrouped === 1 ? 'it' : 'them'}.`,
      )
    if (draft.groups.length !== cardColumns)
      problems.push(
        `Use groups needs ${String(cardColumns)} groups, one for each card column; this set has ${String(draft.groups.length)}.`,
      )
    draft.groups.forEach((group, column) => {
      const needed = rows - (column === freeColumn ? 1 : 0)
      const has = draft.entries.filter((entry) => entry.groupId === group.id).length
      if (column < cardColumns && has < needed)
        problems.push(
          `“${group.name}” has ${String(has)} ${has === 1 ? 'entry' : 'entries'}; its column needs at least ${String(needed)}.`,
        )
    })
  }
  return problems
}
