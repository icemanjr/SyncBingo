import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { GameEnded } from './GameEnded'

describe('a game that has ended', () => {
  it('shows the final results to players when the host ended it', () => {
    render(
      <MemoryRouter>
        <GameEnded results={[{ participantId: 'a', name: 'Ava', wins: 2 }]} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'Final results' })).toBeInTheDocument()
    expect(screen.getByText('2 wins')).toBeInTheDocument()
  })

  it('shows no results when there are none, such as a game that expired', () => {
    render(
      <MemoryRouter>
        <GameEnded />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('heading', { name: 'Final results' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'See the game summary' })).toBeNull()
  })
})
