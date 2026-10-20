import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { aCall, aHostSnapshot } from '../../test/fixtures'
import { Card } from '../play/Card'
import { defaultMarkStyle } from '../play/markStyle'
import { CallBoard } from './CallBoard'
import { allSquareFaces, ballFace, callFace, squareFace, type Content } from './faces'
import { applyEvent } from './gameEvents'
import { LastBall } from './LastBall'

/** A custom set (ADR 0009): item n calls "n + 1" and is marked "n+1"; item 3 is a long definition. */
const sums: Content = {
  name: 'Sums',
  groupItems: false,
  items: Array.from({ length: 30 }, (_, i) =>
    i === 2
      ? { ball: 'The organelle that turns food into energy for the cell', square: 'mitochondria' }
      : { ball: `${String(i + 1)} + 1`, square: String(i + 2) },
  ),
}

describe('faces (ADR 0009)', () => {
  it('reads an item’s ball and square text from the set, or the number without one', () => {
    expect(ballFace(sums, 1)).toBe('1 + 1')
    expect(squareFace(sums, 1)).toBe('2')
    expect(ballFace(null, 37)).toBe('37')
    expect(squareFace(undefined, 37)).toBe('37')
  })

  it('takes a call’s text from the call itself', () => {
    expect(callFace({ ...aCall(5), call: '5 + 1', square: '6' })).toBe('5 + 1')
    expect(callFace(aCall(5))).toBe('5')
  })

  it('lists every square face on the board, for Max text size', () => {
    expect(allSquareFaces(sums, [{ balls: [1, 2, 3] }])).toEqual(['2', '3', 'mitochondria'])
  })
})

describe('a card with a custom set', () => {
  it('shows each square’s text and names it for screen readers', () => {
    render(
      <Card
        card={{ cardId: 1, cells: [1, 3, 0, 4], marks: 0 }}
        columns={2}
        columnLabels={null}
        freeCell={2}
        called={null}
        highlightMatches={false}
        markStyle={defaultMarkStyle}
        setFaces={[]}
        content={sums}
        disabled={false}
        onToggle={() => undefined}
      />,
    )

    expect(screen.getByRole('gridcell', { name: '2' })).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'mitochondria' })).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'Free space' })).toBeInTheDocument()
    expect(screen.getByRole('grid')).toHaveClass('gap-1') // tighter, for room
  })

  it('fits a long group name above its column', () => {
    render(
      <Card
        card={{ cardId: 1, cells: [1, 2], marks: 0 }}
        columns={2}
        columnLabels={['Week 1 words', '×2']}
        freeCell={null}
        called={null}
        highlightMatches={false}
        markStyle={defaultMarkStyle}
        setFaces={[]}
        content={sums}
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
        last={{
          ...aCall(3),
          letter: 'Biology',
          call: 'The organelle that turns food into energy for the cell',
          square: 'mitochondria',
        }}
      />,
    )

    const last = screen.getByTestId('last-ball')
    expect(last).toHaveTextContent('Biology The organelle that turns food into energy for the cell')
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
    expect(
      within(balls).getByTitle('The organelle that turns food into energy for the cell'),
    ).toHaveClass('truncate')
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
