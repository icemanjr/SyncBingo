import { describe, expect, it } from 'vitest'
import { describeChange } from './rules'

const noPatterns = { patterns: [], presets: [] }

describe('rule sentences (FR-47, FR-48)', () => {
  const off = (rule: string) => describeChange({ rule, value: 'false' }, noPatterns)

  it('state the rule as it stands, so they read right in the current rules too', () => {
    expect(off('requireMarksForClaims')).toBe('You can call Bingo without marks')
    expect(off('highlightMatches')).toBe('Called squares are not highlighted')
    expect(off('showBallToPlayers')).toBe('The called ball is not shown on your screen')
  })

  it('never say "no longer", which only fits a change', () => {
    const rules = [
      'showBallToPlayers',
      'highlightMatches',
      'showCallBoard',
      'autoFillAvailable',
      'freeMarking',
      'requireMarksForClaims',
    ]
    for (const rule of rules) {
      for (const value of ['true', 'false']) {
        expect(describeChange({ rule, value }, noPatterns)).not.toMatch(/no longer/i)
      }
    }
  })

  it('say which free space the cards have', () => {
    const free = (value: string) => describeChange({ rule: 'freeSpace', value }, noPatterns)
    expect(free('AlwaysMarked')).toBe('The free space is always marked')
    expect(free('Earned')).toBe('The free space must be earned')
    expect(free('None')).toBe('No free space: every square is a ball')
  })
})
