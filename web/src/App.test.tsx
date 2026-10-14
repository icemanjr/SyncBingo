import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { AppRoutes } from './App'

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AppRoutes />
    </MemoryRouter>,
  )
}

describe('the landing page', () => {
  it('offers hosting and joining', () => {
    renderAt('/')

    expect(screen.getByRole('heading', { level: 1, name: 'SyncBingo' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Host a game' })).toHaveAttribute('href', '/host/new')
    expect(screen.getByRole('link', { name: 'Join a game' })).toHaveAttribute('href', '/join')
  })

  it('shows Serious Bingo as coming soon, not as something to click', () => {
    renderAt('/')

    const tile = screen.getByText('Serious Bingo').parentElement
    expect(tile).toHaveAttribute('aria-disabled', 'true')
    expect(screen.queryByRole('link', { name: /serious/i })).not.toBeInTheDocument()
  })

  it('reserves ad slots around the page, never over it', () => {
    renderAt('/')

    const slots = screen.getAllByRole('complementary', { name: 'Advertisement' })
    expect(slots.map((slot) => slot.dataset.adSlot).sort()).toEqual([
      'bottom',
      'left',
      'right',
      'top',
    ])
  })
})

describe('the display view', () => {
  it('has no ads', () => {
    renderAt('/display/9ve4shp3k7mwa2qx')

    expect(screen.queryByRole('complementary', { name: 'Advertisement' })).not.toBeInTheDocument()
  })
})
