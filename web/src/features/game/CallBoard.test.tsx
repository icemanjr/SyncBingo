import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { aCall, aGame } from '../../test/fixtures'
import { CallBoard } from './CallBoard'

describe('the call board (FR-27)', () => {
  const board = aGame().board

  it('fills each letter down its columns: 1-5 in the first column, then 6-10', () => {
    render(<CallBoard board={board} calls={[]} />)

    const b = screen.getByRole('list', { name: 'Balls B' })
    // The grid flows down 5 rows, then across, so the balls keep their order 1, 2, 3… in the markup.
    expect(b).toHaveClass('grid-flow-col')
    expect(b).toHaveStyle({ gridTemplateRows: 'repeat(5, auto)' })
    expect(
      within(b)
        .getAllByRole('listitem')
        .map((ball) => ball.textContent),
    ).toEqual(Array.from({ length: 15 }, (_, i) => String(i + 1)))
  })

  it('shows the Last Ball with its letter', () => {
    render(<CallBoard board={board} calls={[aCall(27)]} />)

    expect(screen.getByText('Last Ball')).toBeInTheDocument()
    expect(screen.getByTestId('last-ball')).toHaveTextContent('I 27')
    expect(screen.getByRole('listitem', { name: 'I 27, called' })).toBeInTheDocument()
  })
})
