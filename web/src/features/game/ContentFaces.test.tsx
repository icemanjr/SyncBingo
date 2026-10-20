import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { aCall, aHostSnapshot } from '../../test/fixtures'
import { Card } from '../play/Card'
import { defaultMarkStyle } from '../play/markStyle'
import { CallBoard } from './CallBoard'
import { allSquareFaces, ballFace, calledTest, callFace, cardFace, type Content } from './faces'
import { applyEvent } from './gameEvents'
import { LastBall } from './LastBall'

const definition = 'The organelle that turns food into energy for the cell'

/** A custom set (ADR 0009) as a client sees it: ball text by item, squares alphabetical. */
const sums: Content = {
  name: 'Sums',
  groupItems: false,
  board: Array.from({ length: 30 }, (_, i) => (i === 2 ? definition : `${String(i + 1)} + 1`)),
  squares: ['10', '2', 'mitochondria'],
  labels: [],
}

/** A player's card in a set game: placeholder cells (positions), the text in faces. */
const textCard = { cardId: 1, cells: [1, 2, 0, 4], marks: 0, faces: ['2', 'mitochondria', '', '5'] }

describe('faces (ADR 0009)', () => {
  it('reads a ball’s text from the board, or the number without a set', () => {
    expect(ballFace(sums, 1)).toBe('1 + 1')
    expect(ballFace(null, 37)).toBe('37')
  })

  it('takes a call’s text from the call itself', () => {
    expect(callFace({ ...aCall(5), call: '5 + 1', square: '6' })).toBe('5 + 1')
    expect(callFace(aCall(5))).toBe('5')
  })

  it('takes a square’s text from the card itself, or its number', () => {
    expect(cardFace(textCard, 1)).toBe('mitochondria')
    expect(cardFace({ cardId: 1, cells: [7, 22], marks: 0 }, 1)).toBe('22')
  })

  it('sizes Max text from the set’s squares, or the board’s numbers', () => {
    expect(allSquareFaces(sums, [{ balls: [1, 2, 3] }])).toEqual(['10', '2', 'mitochondria'])
    expect(allSquareFaces(null, [{ balls: [1, 2] }])).toEqual(['1', '2'])
  })

  it('knows a set card’s square was called by its text, since the card holds no items', () => {
    const called = calledTest(textCard, [{ ...aCall(3), call: definition, square: 'mitochondria' }])

    expect(called?.(1)).toBe(true)
    expect(called?.(0)).toBe(false)
    expect(calledTest(textCard, null)).toBeNull()
  })

  it('knows a number card’s square was called by its number', () => {
    const called = calledTest({ cardId: 1, cells: [7, 22], marks: 0 }, [aCall(22)])

    expect(called?.(1)).toBe(true)
    expect(called?.(0)).toBe(false)
  })
})

describe('a card with a custom set', () => {
  it('shows each square’s text, names it for screen readers and outlines called ones', () => {
    render(
      <Card
        card={textCard}
        columns={2}
        columnLabels={null}
        freeCell={2}
        isCalled={(cell) => cell === 1}
        highlightMatches
        markStyle={defaultMarkStyle}
        setFaces={[]}
        disabled={false}
        onToggle={() => undefined}
      />,
    )

    expect(screen.getByRole('gridcell', { name: '2' })).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'mitochondria, called' })).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'Free space' })).toBeInTheDocument()
    expect(screen.getByRole('grid')).toHaveClass('gap-1') // tighter, for room
  })

  it('fits a long group name above its column', () => {
    render(
      <Card
        card={{ cardId: 1, cells: [1, 2], marks: 0, faces: ['a', 'b'] }}
        columns={2}
        columnLabels={['Week 1 words', '×2']}
        freeCell={null}
        isCalled={null}
        highlightMatches={false}
        markStyle={defaultMarkStyle}
        setFaces={[]}
        disabled={false}
        onToggle={() => undefined}
      />,
    )

    expect(screen.getByTitle('Week 1 words')).toHaveClass('text-sm')
    expect(screen.getByTitle('×2')).toHaveClass('text-xl')
  })
})

describe('the last ball with a custom set', () => {
  it('shows the call’s text, with its group, in a panel rather than a ball', () => {
    render(
      <LastBall
        last={{ ...aCall(3), letter: 'Biology', call: definition, square: 'mitochondria' }}
      />,
    )

    const last = screen.getByTestId('last-ball')
    expect(last).toHaveTextContent(`Biology ${definition}`)
    expect(last).toHaveClass('rounded-3xl')
    expect(last).not.toHaveClass('rounded-full')
  })

  it('shows no label when the set is dealt free-form', () => {
    render(<LastBall last={{ ...aCall(1), letter: '', call: '1 + 1', square: '2' }} />)

    expect(screen.getByTestId('last-ball')).toHaveTextContent(/^1 \+ 1$/)
  })
})

describe('the call board with a custom set', () => {
  it('shows each item’s ball text, called ones marked, in one block when free-form', () => {
    render(
      <CallBoard
        board={[{ label: '', balls: Array.from({ length: 30 }, (_, i) => i + 1) }]}
        calls={[{ ...aCall(1), letter: '', call: '1 + 1', square: '2' }]}
        content={sums}
      />,
    )

    const balls = screen.getByRole('region', { name: 'Balls' })
    expect(within(balls).getAllByRole('listitem')).toHaveLength(30)
    expect(within(balls).getByRole('listitem', { name: '1 + 1, called' })).toBeInTheDocument()
    expect(within(balls).getByRole('listitem', { name: '2 + 1' })).toBeInTheDocument()
    expect(within(balls).getByTitle(definition)).toHaveClass('truncate')
  })

  it('shows each group under its name when dealt by group', () => {
    render(
      <CallBoard
        board={[
          { label: '×1', balls: [1, 2] },
          { label: '×2', balls: [3, 4] },
        ]}
        calls={[]}
        content={sums}
        showLastBall={false}
      />,
    )

    expect(screen.getByRole('heading', { name: '×1' })).toBeInTheDocument()
    expect(
      within(screen.getByRole('region', { name: 'Balls ×2' })).getAllByRole('listitem'),
    ).toHaveLength(2)
  })
})

describe('changing the set', () => {
  it('clears the calls and fetches the new board and cards', () => {
    const before = aHostSnapshot({ calls: [aCall(3)] })

    const { snapshot, refetch } = applyEvent(before, {
      type: 'ContentChanged',
      seq: 2,
      data: { name: 'Sums', items: 30, grouped: false },
    })

    expect(snapshot.game.calls).toEqual([])
    expect(refetch).toBe(true)
  })
})
