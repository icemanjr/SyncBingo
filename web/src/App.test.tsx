import userEvent from '@testing-library/user-event'
import { render, screen, within } from '@testing-library/react'
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
      screen.getByRole('heading', { level: 1, name: 'Bingo that keeps everyone in sync.' }),
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

describe('where ads may appear (ADR 0012)', () => {
  const slots = () => screen.queryAllByRole('complementary', { name: 'Advertisement' })

  it.each(['/', '/how-to-play', '/faq'])('pages with content have ad slots: %s', (path) => {
    renderAt(path)

    expect(slots()).toHaveLength(4)
  })

  it.each([
    '/join',
    '/join/9ve4shp3k7mwa2qx',
    '/host/new',
    '/host/9ve4shp3k7mwa2qx/set',
    '/terms',
    '/privacy',
    '/pair/k7m2qx',
    '/no-such-page',
  ])('forms, legal pages and dead ends have none: %s', (path) => {
    renderAt(path)

    expect(slots()).toHaveLength(0)
  })

  it('a game screen shows none while it is a dead end, such as a game this device never joined', () => {
    renderAt('/play/9ve4shp3k7mwa2qx')

    expect(screen.getByText('You have not joined this game on this device.')).toBeInTheDocument()
    expect(slots()).toHaveLength(0)
  })
})

describe('the guides', () => {
  it('How to play covers players and hosts, with the patterns drawn and the glossary', () => {
    renderAt('/how-to-play')

    expect(
      screen.getByRole('heading', { level: 1, name: 'How to play SyncBingo' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'For players' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'For hosts' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Four Corners' })).toBeInTheDocument()
    expect(screen.getByText('Waiting room')).toBeInTheDocument()
    expect(document.title).toBe('How to play SyncBingo · SyncBingo')
  })

  it('the FAQ answers whether the host can play', () => {
    renderAt('/faq')

    expect(
      screen.getByRole('heading', { level: 2, name: 'Can the host play too?' }),
    ).toBeInTheDocument()
  })

  it('the landing page explains SyncBingo and its words, and every page links the guides', () => {
    renderAt('/')

    expect(screen.getByRole('heading', { name: 'What is SyncBingo?' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Words SyncBingo uses' })).toBeInTheDocument()
    expect(screen.getByText(/The host doesn’t play/)).toBeInTheDocument()
    const footer = screen.getByRole('contentinfo')
    expect(within(footer).getByRole('link', { name: 'How to play' })).toHaveAttribute(
      'href',
      '/how-to-play',
    )
    expect(within(footer).getByRole('link', { name: 'FAQ' })).toHaveAttribute('href', '/faq')
  })
})

describe('an address the site does not have', () => {
  it('says the page was not found and links back to the start', () => {
    renderAt('/no-such-page')

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to the start' })).toHaveAttribute('href', '/')
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
