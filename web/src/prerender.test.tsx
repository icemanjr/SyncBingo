import { readdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { librarySets } from './features/guide/setLibrary'
import { readSetFile } from './features/sets/setFile'
import { setProblems } from './features/sets/draft'
import { prebuiltPages, render } from './prerender'

describe('pre-built pages (docs/07)', () => {
  const headings: Record<string, string> = {
    '/': 'What is SyncBingo?',
    '/how-to-play': 'How to play SyncBingo',
    '/faq': 'Frequently asked questions',
    '/sets': 'Ready-made sets',
    '/terms': 'Terms of Use',
    '/privacy': 'Privacy',
  }

  it.each(prebuiltPages.map((page) => page.path))(
    '%s renders its own content without a browser session',
    (path) => {
      const html = render(path)

      expect(html).toContain(headings[path])
      expect(html).not.toContain('Page not found')
    },
  )

  it('gives every page its own title and description', () => {
    expect(new Set(prebuiltPages.map((page) => page.title)).size).toBe(prebuiltPages.length)
    expect(new Set(prebuiltPages.map((page) => page.description)).size).toBe(prebuiltPages.length)
  })
})

describe('the ready-made sets', () => {
  const folder = resolve(process.cwd(), 'public/sets')
  const read = (file: string) => readSetFile(readFileSync(resolve(folder, file), 'utf8'))

  it('lists every set file there is', () => {
    expect(librarySets.map((set) => set.file).sort()).toEqual(
      readdirSync(folder)
        .filter((file) => file.endsWith('.json'))
        .sort(),
    )
  })

  it.each(librarySets)('$name is a valid set for a 5 × 5 card, as listed', (set) => {
    const result = read(set.file)
    if ('error' in result) throw new Error(result.error)
    const { draft } = result

    expect(draft.entries).toHaveLength(set.entries)
    expect(draft.groups.length > 0).toBe(set.grouped)
    expect(setProblems(draft, set.grouped)).toEqual([])
    for (const [ball, square] of set.examples)
      expect(draft.entries).toContainEqual(expect.objectContaining({ ball, square }))
  })
})
