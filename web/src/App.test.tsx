import userEvent from '@testing-library/user-event'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AppRoutes } from './App'

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AppRoutes />
    </MemoryRouter>,
  )
}

describe('the landing page', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('offers hosting and joining', () => {
    renderAt('/')

    expect(
      screen.getByRole('heading', { level: 1, name: 'Bingo for a room full of phones.' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Host a game' })).toHaveAttribute('href', '/host/new')
    expect(screen.getByRole('link', { name: 'Join a game' })).toHaveAttribute('href', '/join')
  })

  it('makes no promise of Serious Bingo on the live site', () => {
    renderAt('/')

    expect(screen.queryByText('Serious Bingo')).not.toBeInTheDocument()
  })

  it('in the Full portfolio build, shows Serious Bingo as coming soon, not as something to click', () => {
    vi.stubEnv('VITE_SERIOUS_BINGO_TEASER', 'true')
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

describe('the legal pages', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('are linked from every page, and say they are drafts', async () => {
    renderAt('/')

    await userEvent.click(screen.getByRole('link', { name: 'Privacy' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Privacy' })).toBeInTheDocument()
    expect(screen.getByRole('note')).toHaveTextContent('Draft, pending legal review')
    await userEvent.click(screen.getByRole('link', { name: 'Terms' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Terms of Use' })).toBeInTheDocument()
  })

  it('name the operator and the addresses from the build settings', () => {
    vi.stubEnv('VITE_OPERATOR_NAME', 'Example Games Ltd')
    vi.stubEnv('VITE_LEGAL_EMAIL', 'legal@example.com')
    vi.stubEnv('VITE_SUPPORT_EMAIL', 'help@example.com')
    renderAt('/terms')

    expect(screen.getByText(/run by Example Games Ltd/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'legal@example.com' })).toHaveAttribute(
      'href',
      'mailto:legal@example.com',
    )
    expect(screen.getByRole('link', { name: 'help@example.com' })).toHaveAttribute(
      'href',
      'mailto:help@example.com',
    )
  })

  it('use the live site’s settings by default', () => {
    renderAt('/privacy')

    expect(screen.getByText(/run by Nathan Holst/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'legal@syncbingo.com' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'support@syncbingo.com' })).toBeInTheDocument()
  })
})
