import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import {
  addEntry,
  checkEntry,
  describe as describeProblem,
  describeMerge,
  editEntry,
  emptyDraft,
  fitGroups,
  mergeDraft,
  moveEntry,
  moveGroup,
  removeUngrouped,
  renameGroup,
  setLabel,
  setProblems,
  ungroupedOf,
  type Draft,
} from './draft'
import { readSetFile, writeSetFile } from './setFile'
import { defaultLabel, fits, isPrintable, length, tidy } from './text'

// Tests run from web/; the sample sets live beside it (samples/README.md).
const sample = readFileSync(resolve(process.cwd(), '../samples/science-vocabulary.json'), 'utf8')

/** The value, or a failed test saying it was missing (instead of a `!`). */
function must<T>(value: T | undefined | null): T {
  if (value === undefined || value === null) throw new Error('expected a value')
  return value
}

/** A draft of `count` word-list entries, "word 1", "word 2", … */
function words(count: number): Draft {
  let draft = emptyDraft()
  for (let i = 1; i <= count; i++) draft = addEntry(draft, `word ${String(i)}`, null, null).draft
  return draft
}

/** A draft with one group per name, as Use groups makes them (named in order). */
function grouped(...names: string[]): Draft {
  const draft = fitGroups(emptyDraft(), names.length)
  return {
    ...draft,
    groups: draft.groups.map((group, i) => {
      const name = names[i] ?? group.name
      return { ...group, name, label: defaultLabel(name) }
    }),
  }
}

const read = (text: string): Draft => {
  const result = readSetFile(text)
  if ('error' in result) throw new Error(result.error)
  return result.draft
}

describe('text rules (as the server applies them)', () => {
  it('tidies spacing and look-alike forms', () => {
    expect(tidy('  carbon\t\tdioxide ')).toBe('carbon dioxide')
    expect(tidy('７')).toBe('7')
  })

  it('counts characters as a reader does, and stops stacked accents', () => {
    expect(length('👍🏽')).toBe(1)
    expect(fits('a' + '́'.repeat(60), 15)).toBe(false)
  })

  it('allows symbols, scripts and emoji, not control or formatting characters', () => {
    expect(isPrintable('2 × 3 ÷ 4')).toBe(true)
    expect(isPrintable('👨‍👩‍👧')).toBe(true)
    expect(isPrintable('right‮left')).toBe(false)
    expect(isPrintable('bell\u0007')).toBe(false)
  })

  it('labels a group with its first letter, upper case', () => {
    expect(defaultLabel('Biology')).toBe('B')
    expect(defaultLabel('élan')).toBe('É')
    expect(defaultLabel('×2')).toBe('×')
  })
})

describe('entries', () => {
  it('adds a matched entry as one text, and a pair as two', () => {
    let draft = addEntry(emptyDraft(), ' cat ', null, null).draft
    draft = addEntry(draft, '2 + 5', '7', null).draft

    expect(draft.entries.map((e) => [e.ball, e.square])).toEqual([
      ['cat', null],
      ['2 + 5', '7'],
    ])
  })

  it('refuses a duplicate, and a ball or square already used, ignoring case', () => {
    const draft = addEntry(
      addEntry(emptyDraft(), 'Cat', null, null).draft,
      '2 + 5',
      '7',
      null,
    ).draft

    expect(checkEntry(draft, 'cat', null)?.kind).toBe('duplicate')
    expect(describeProblem(must(checkEntry(draft, '3 + 4', '7')))).toBe(
      '“7” is already a square, for “2 + 5”.',
    )
    expect(describeProblem(must(checkEntry(draft, '2 + 5', '8')))).toBe(
      '“2 + 5” is already called for “7”.',
    )
  })

  it('keeps squares within 15 characters and balls within 120', () => {
    expect(checkEntry(emptyDraft(), 'a clue', 'sixteen letters!')?.kind).toBe('tooLong')
    expect(checkEntry(emptyDraft(), 'Mississippi River', null)).toMatchObject({
      kind: 'tooLong',
      side: 'square',
    })
    expect(checkEntry(emptyDraft(), 'd'.repeat(121), 'ok')).toMatchObject({
      kind: 'tooLong',
      side: 'ball',
    })
  })

  it('edits an entry, checked against every other entry but itself', () => {
    const draft = words(3)
    const id = must(draft.entries[0]).id

    expect(editEntry(draft, id, 'word 1', 'first').problem).toBeUndefined()
    expect(editEntry(draft, id, 'word 2', null).problem?.kind).toBe('duplicate')
  })
})

describe('groups', () => {
  it('fits the groups to the card: one per column, named by its letter', () => {
    const draft = fitGroups(emptyDraft(), 5)

    expect(draft.groups.map((g) => [g.name, g.label])).toEqual([
      ['B', 'B'],
      ['I', 'I'],
      ['N', 'N'],
      ['G', 'G'],
      ['O', 'O'],
    ])
  })

  it('adds empty groups at the end when the card has more columns than the set has groups', () => {
    let draft = grouped('Biology', 'Geology', 'Physics')
    draft = { ...draft, groups: draft.groups.slice(0, 3) }

    const fitted = fitGroups(draft, 5)

    expect(fitted.groups.map((g) => g.name)).toEqual(['Biology', 'Geology', 'Physics', 'G', 'O'])
    expect(setProblems(fitted, true)).toContain('“G” has 0 entries; its column needs at least 5.')
  })

  it('makes the pairs of groups past the last column ungrouped, for the host to move or remove', () => {
    const draft = read(sample)

    const fitted = fitGroups(draft, 3)

    expect(fitted.groups.map((g) => g.label)).toEqual(['B', 'I', 'N'])
    expect(fitted.entries).toHaveLength(75)
    expect(ungroupedOf(fitted)).toHaveLength(30)
    expect(setProblems(fitted, true, 3, 1, 3)).toEqual([
      '30 ungrouped pairs: move them into a group, or remove them.',
    ])

    const trimmed = removeUngrouped(fitted)
    expect(trimmed.entries).toHaveLength(45)
    expect(setProblems(trimmed, true, 3, 1, 3)).toEqual([])
  })

  it('renames a group for all its entries, and its default label follows the name', () => {
    let draft = grouped('Biology')
    const group = must(draft.groups[0]).id
    draft = addEntry(draft, 'cell', null, group).draft

    draft = renameGroup(draft, group, 'Life science').draft

    expect(draft.groups[0]).toMatchObject({ name: 'Life science', label: 'L' })
    expect(must(draft.entries[0]).groupId).toBe(group)
    const two = grouped('A', 'B')
    expect(renameGroup(two, must(two.groups[0]).id, 'b').problem).toBe(
      'There is already a group called “b”.',
    )
  })

  it('keeps a label the host chose when the group is renamed', () => {
    let draft = grouped('Biology')
    const group = must(draft.groups[0]).id
    draft = setLabel(draft, group, 'Bio').draft

    expect(must(renameGroup(draft, group, 'Life science').draft.groups[0]).label).toBe('Bio')
    expect(setLabel(draft, group, 'Biology').problem).toBe('A label can be up to 5 characters.')
    expect(must(setLabel(draft, group, '  ').draft.groups[0]).label).toBe('B')
  })

  it('reorders groups and moves entries between them, or out to the ungrouped pairs', () => {
    let draft = grouped('A', 'B')
    const [a, b] = draft.groups.map((g) => g.id) as [string, string]
    draft = addEntry(draft, 'x', null, a).draft

    draft = moveGroup(draft, b, -1)
    draft = moveEntry(draft, must(draft.entries[0]).id, b)

    expect(draft.groups.map((g) => g.name)).toEqual(['B', 'A'])
    expect(must(draft.entries[0]).groupId).toBe(b)
    expect(moveGroup(draft, b, -1)).toBe(draft) // already first
    expect(ungroupedOf(moveEntry(draft, must(draft.entries[0]).id, null))).toHaveLength(1)
  })
})

describe('add from file (FR-44)', () => {
  it('merges groups by name and skips duplicates and clashes, reporting them', () => {
    let week1 = { ...grouped('Words'), name: 'Week 1' }
    week1 = addEntry(week1, 'cat', null, must(week1.groups[0]).id).draft
    week1 = addEntry(week1, 'a pet that barks', 'dog', must(week1.groups[0]).id).draft
    const week2 = read(
      '{ "format": 1, "name": "Week 2", "groups": [ { "name": "words", "pairs": [ ["CAT"], ["a farm bird", "dog"], ["sun"] ] }, { "name": "New", "pairs": [ ["moon"] ] } ] }',
    )

    const { draft, report } = mergeDraft(week1, week2)

    expect(draft.name).toBe('Week 1')
    expect(draft.groups.map((g) => g.name)).toEqual(['Words', 'New'])
    expect(draft.entries.map((e) => e.ball)).toEqual(['cat', 'a pet that barks', 'sun', 'moon'])
    expect(report).toEqual({
      added: 2,
      duplicates: 1,
      clashes: [{ ball: 'a farm bird', square: 'dog' }],
    })
    expect(describeMerge(report)).toBe(
      'Added 2. 1 duplicate skipped; 1 clashes with existing entries: “a farm bird” / “dog”.',
    )
  })
})

describe('what stops a draft being used', () => {
  it('needs 24 to 500 entries', () => {
    expect(setProblems(words(23), false)).toEqual(['A set needs 24–500 entries; this one has 23.'])
    expect(setProblems(words(24), false)).toEqual([])
  })

  it('needs one more entry on a card without a free space', () => {
    expect(setProblems(words(24), false, 5, null)).toEqual([
      'This card needs at least 25 entries; the set has 24.',
    ])
    expect(setProblems(words(25), false, 5, null)).toEqual([])
  })

  it('with Use groups, needs five groups that fill their columns and every entry grouped', () => {
    const draft = read(sample)
    expect(setProblems(draft, true)).toEqual([])

    const loose = addEntry(draft, 'an extra clue', 'Extra', null).draft
    expect(setProblems(loose, true)).toEqual([
      '1 ungrouped pair: move it into a group, or remove it.',
    ])
    expect(setProblems(loose, false)).toEqual([])
  })
})

describe('the set file', () => {
  it('reads the sample set: five groups labelled B-I-N-G-O, 75 clues and words', () => {
    const draft = read(sample)

    expect(draft.name).toBe('Science vocabulary (sample)')
    expect(draft.groups.map((g) => g.label)).toEqual(['B', 'I', 'N', 'G', 'O'])
    expect(draft.entries).toHaveLength(75)
    expect(draft.entries[0]).toMatchObject({
      ball: 'An organism that makes its own food using sunlight.',
      square: 'Producer',
    })
  })

  it('writes the same layout as the server, one pair to a line', () => {
    let draft = { ...grouped('words'), name: 'Mixed' }
    draft = addEntry(draft, 'word 1', null, must(draft.groups[0]).id).draft
    draft = addEntry(draft, '2 × 3', '6', null).draft

    expect(writeSetFile(draft)).toBe(
      '{\n  "format": 1,\n  "name": "Mixed",\n  "groups": [\n    {\n      "name": "words",\n      "label": "W",\n      "pairs": [\n        ["word 1"]\n      ]\n    }\n  ],\n  "pairs": [\n    ["2 × 3", "6"]\n  ]\n}\n',
    )
  })

  it('writes no groups without Use groups, so the set is dealt anywhere', () => {
    const file = writeSetFile(read(sample), false)

    expect(read(file).groups).toEqual([])
    expect(read(file).entries).toHaveLength(75)
  })

  it('round-trips the sample set', () => {
    const once = writeSetFile(read(sample))

    expect(writeSetFile(read(once))).toBe(once)
  })

  it('refuses what isn’t a set file, as the server does', () => {
    for (const bad of [
      'not json',
      '[]',
      '{ "name": "no format" }',
      '{ "format": 1 }',
      '{ "format": 1, "pairs": [ ["a", "b", "c"] ] }',
      '{ "format": 1, "pairs": [ { "ball": "old" } ] }',
    ]) {
      expect(readSetFile(bad)).toEqual({
        error:
          "This isn't a SyncBingo set file. Download one from the set editor to see the format.",
      })
    }
    expect(readSetFile('{ "format": 2, "pairs": [] }')).toEqual({
      error:
        'This set file was made by a newer version of SyncBingo; reload the page and try again.',
    })
    expect(readSetFile(`{ "format": 1, "pairs": [], "pad": "${'x'.repeat(70_000)}" }`)).toEqual({
      error: 'A set file can be at most 64 KB.',
    })
  })
})
