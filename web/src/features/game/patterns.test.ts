import { describe, expect, it } from 'vitest'
import { aGame } from '../../test/fixtures'
import { keyOf, selectionName, winMessage } from './patterns'

const data = aGame()

describe('pattern selections (FR-21)', () => {
  it('names a selection, using a preset when all its patterns are selected', () => {
    expect(selectionName('rows+columns+diagonals', data)).toBe('Normal Bingo')
    expect(selectionName('rows+columns+diagonals+corners', data)).toBe(
      'Normal Bingo or Four Corners',
    )
    expect(selectionName('rows+diagonals', data)).toBe('Rows or Diagonals')
    expect(selectionName('rows+columns+diagonals+B+blackout', data)).toBe(
      'Normal Bingo, Letter B or Blackout',
    )
  })

  it("keys a selection in the variant's order, whatever order it was picked in", () => {
    expect(keyOf(['corners', 'rows'], data)).toBe('rows+corners')
  })
})

describe('win announcements', () => {
  it('says "a Bingo" for lines and names a shape the server announced', () => {
    expect(winMessage('Ava', null, data, false)).toBe('Ava had a Bingo!')
    expect(winMessage('Ava', 'corners', data, false)).toBe('Ava had Four Corners!')
    expect(winMessage('Ava', 'B', data, false)).toBe('Ava had the letter B!')
  })

  it('speaks to the winner', () => {
    expect(winMessage('Ava', 'corners', data, true)).toBe('🎉 You have Four Corners!')
    expect(winMessage('Ava', null, data, true)).toBe('🎉 You have Bingo!')
  })
})
