import type { Schemas } from '../../api/client'

type Pattern = Schemas['PatternView']

/** What naming a selection needs: the variant's patterns and presets, as the snapshot sends them. */
export interface PatternData {
  patterns: Pattern[]
  presets: Schemas['PresetView'][]
}

/** Separates pattern ids in a selection key ("rows+corners"), as the server does (FR-21). */
export const Separator = '+'

/** The ids in a selection key, e.g. "rows+corners" → rows, corners. */
export const idsOf = (key: string): string[] => key.split(Separator)

/** The selected patterns, in the variant's order. */
export function selectedPatterns(key: string, data: PatternData): Pattern[] {
  const ids = new Set(idsOf(key))
  return data.patterns.filter((pattern) => ids.has(pattern.id))
}

/** The key for a set of pattern ids, in the variant's order, so the same selection always has the same key. */
export function keyOf(ids: Iterable<string>, data: PatternData): string {
  const wanted = new Set(ids)
  return data.patterns
    .filter((pattern) => wanted.has(pattern.id))
    .map((pattern) => pattern.id)
    .join(Separator)
}

/** "A", "A or B", "A, B or C". */
export function joinOr(names: string[]): string {
  return names.length <= 1
    ? (names[0] ?? '')
    : `${names.slice(0, -1).join(', ')} or ${names[names.length - 1] ?? ''}`
}

/**
 * A selection's name: a preset whose patterns are all selected reads as its name ("Normal Bingo"),
 * the rest by their own names: "Normal Bingo or Four Corners", "Rows or Diagonals".
 */
export function selectionName(key: string, data: PatternData): string {
  const left = new Set(idsOf(key))
  const names: string[] = []
  for (const preset of data.presets) {
    if (preset.patternIds.every((id) => left.has(id))) {
      names.push(preset.name)
      for (const id of preset.patternIds) left.delete(id)
    }
  }

  for (const pattern of data.patterns) {
    if (left.has(pattern.id)) names.push(pattern.name)
  }

  return joinOr(names) || key
}

/**
 * What everyone is told about a win: "Ava had a Bingo!" for lines, or the shape when it was the only thing
 * complete ("Ava had Four Corners!"); the server decides which, so it never names a pattern the player
 * didn't declare.
 */
export function winMessage(
  name: string,
  announced: string | null | undefined,
  data: PatternData,
  mine: boolean,
): string {
  const shape = data.patterns.find((pattern) => pattern.id === announced)?.announcedAs
  if (mine) return shape ? `🎉 You have ${shape}!` : '🎉 You have Bingo!'
  return shape ? `${name} had ${shape}!` : `${name} had a Bingo!`
}

/** The names of completed patterns, for the host ("Rows, Four Corners"). */
export function patternNames(ids: string[] | null | undefined, data: PatternData): string {
  return (ids ?? [])
    .map((id) => data.patterns.find((pattern) => pattern.id === id)?.name ?? id)
    .join(', ')
}
