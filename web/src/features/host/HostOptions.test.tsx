import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { seats } from '../../storage/sessions'
import { aHostSnapshot, standardPatterns } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { RulesNotice } from '../game/RulesNotice'
import { HostConsole } from './HostConsole'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`
const hostSettings = {
  autoCallIntervalSeconds: 10,
  showBallToPlayers: true,
  highlightMatches: false,
  showCallBoard: true,
  autoFillAvailable: false,
  winHighlight: true,
  winDetection: 'AlertHost' as const,
  redrawPolicy: 'HostControls' as const,
  freeMarking: true,
  requireMarksForClaims: false,
  unmarking: 'Any' as const,
  freeSpace: 'AlwaysMarked' as const,
  cardSize: 'FiveByFive' as const,
  showRoundNumbers: true,
  multipleWins: 'OncePerPattern' as const,
}

function renderConsole() {
  render(
    <MemoryRouter initialEntries={[`/host/${code}`]}>
      <Routes>
        <Route path="/host/:code" element={<HostConsole />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Options (FR-32)', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('sends only what changed, with the pattern, in one Apply', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Manual', round: 1 }, { settings: hostSettings }),
      },
      [`PUT /api/sessions/${code}/rules`]: {
        body: { settings: hostSettings, winCondition: 'blackout' },
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Mark any square' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Blackout' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Normal Bingo' }))
    await userEvent.click(screen.getByRole('button', { name: 'Apply 2 changes' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/rules'))?.body).toEqual({
        settings: { freeMarking: false },
        winCondition: 'blackout',
      })
    })
    expect(await screen.findByRole('button', { name: 'Applied ✓' })).toBeDisabled()
  })

  it('adds a pattern to the selection, and never lets the last one go', async () => {
    const api = fakeApi({
      [snapshotPath]: { body: aHostSnapshot({}, { settings: hostSettings }) },
      [`PUT /api/sessions/${code}/rules`]: {
        body: { settings: hostSettings, winCondition: 'rows+columns+diagonals+corners' },
      },
    })
    renderConsole()
    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))

    await userEvent.click(screen.getByRole('checkbox', { name: 'Four Corners' }))
    expect(
      screen.getByText(/Any selected pattern wins: Normal Bingo or Four Corners/),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Apply/ }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/rules'))?.body).toEqual({
        settings: null,
        winCondition: 'rows+columns+diagonals+corners',
      })
    })
  })

  it('keeps at least one pattern selected', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ winCondition: 'corners' }, { settings: hostSettings }),
      },
    })
    renderConsole()
    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))

    expect(screen.getByRole('checkbox', { name: /Four Corners/ })).toBeDisabled()
  })

  it('keeps Options open on Escape, so unapplied changes are not lost', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot({}, { settings: hostSettings }) } })
    renderConsole()
    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))

    await userEvent.click(screen.getByRole('checkbox', { name: 'Four Corners' }))
    await userEvent.keyboard('{Escape}')

    expect(screen.getByRole('dialog', { name: 'Options' })).toBeInTheDocument()
  })

  it('locks the between-rounds settings during a round', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Manual', round: 1 }, { settings: hostSettings }),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))

    expect(screen.getByRole('combobox', { name: 'Free space' })).toBeDisabled()
    expect(screen.getByRole('combobox', { name: 'Multiple wins' })).toBeDisabled()
    expect(
      screen.getByText('Change these in the lobby: they shape a whole round.'),
    ).toBeInTheDocument()
  })

  it('is not offered in auto mode', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Auto', round: 1 }, { settings: hostSettings }),
      },
    })
    renderConsole()

    expect(await screen.findByRole('button', { name: 'Pause' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Options' })).not.toBeInTheDocument()
  })

  it('waits during the countdown', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot(
          { state: 'Starting', countdownEndsAt: new Date(Date.now() + 9000).toISOString() },
          { settings: hostSettings },
        ),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Mark any square' }))

    expect(screen.getByRole('button', { name: 'Apply 1 change' })).toBeDisabled()
  })

  it('asks before throwing changes away', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot({}, { settings: hostSettings }) } })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Mark any square' }))
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))

    expect(screen.getByText('Discard 1 change?')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Discard' }))
    expect(screen.queryByRole('region', { name: 'Options' })).not.toBeInTheDocument()
  })
})

describe('Display', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('connects a TV with its code, saves the password note and disconnects a display', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Auto', round: 1 }, { settings: hostSettings }),
      },
      [`GET /api/sessions/${code}/displays`]: {
        body: [{ displayId: 'd1', kind: 'Tv', createdAt: '2026-10-14T19:00:00Z' }],
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Display' }))
    const menu = screen.getByRole('region', { name: 'Displays' })
    await userEvent.type(within(menu).getByLabelText('Connect a TV'), 'k7m-2qx')
    await userEvent.click(within(menu).getByRole('button', { name: 'Connect' }))
    await userEvent.type(within(menu).getByLabelText('Password note'), 'Password: harvest')
    await userEvent.click(within(menu).getByRole('button', { name: 'Save' }))
    await userEvent.click(await within(menu).findByRole('button', { name: 'Disconnect' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/display-pairings'))?.body).toEqual({
        pairingCode: 'k7m-2qx',
      })
      expect(api.sent.find((s) => s.path.endsWith('/join-note'))?.body).toEqual({
        note: 'Password: harvest',
      })
      expect(api.sent.some((s) => s.method === 'DELETE' && s.path.endsWith('/displays/d1'))).toBe(
        true,
      )
    })
  })
})

describe('the leaderboard', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('shows wins and lets the host show it to everyone', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot(
          { leaderboard: [{ participantId: 'p1', name: 'Ava', wins: 2 }] },
          { settings: hostSettings },
        ),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Leaderboard' }))
    expect(screen.getByRole('dialog', { name: 'Leaderboard' })).toHaveTextContent('2 wins')
    await userEvent.click(screen.getByRole('button', { name: 'Show to everyone' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/leaderboard/show'))?.body).toEqual({
        visible: true,
      })
    })
  })
})

describe('the rules notice (FR-47)', () => {
  it('says what changed in plain words, and closes', async () => {
    const onClose = vi.fn()
    render(
      <RulesNotice
        notice={{
          changes: [
            { rule: 'winCondition', value: 'blackout' },
            { rule: 'freeMarking', value: 'false' },
          ],
          nextBallFrom: '',
        }}
        patterns={{ patterns: standardPatterns, presets: [] }}
        onClose={onClose}
      />,
    )

    expect(screen.getByText('Pattern changed to Blackout')).toBeInTheDocument()
    expect(screen.getByText('Only called numbers can be marked')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalled()
  })
})

describe('Card size (FR-26a)', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('offers a smaller card with its own patterns, keeping what it can and dropping the letters', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ winCondition: 'B+corners' }, { settings: hostSettings }),
      },
      [`PUT /api/sessions/${code}/rules`]: {
        body: { settings: hostSettings, winCondition: 'corners' },
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    expect(screen.getByRole('checkbox', { name: 'Letter B' })).toBeChecked()
    await userEvent.selectOptions(screen.getByLabelText('Card size'), '3 × 3')

    expect(screen.queryByRole('checkbox', { name: 'Letter B' })).not.toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /^Four Corners/ })).toBeChecked()
    await userEvent.click(screen.getByRole('button', { name: 'Apply 2 changes' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/rules'))?.body).toEqual({
        settings: { cardSize: 'ThreeByThree' },
        winCondition: 'corners',
      })
    })
  })

  it('has no free space to choose on a 4 × 4 card', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot({}, { settings: hostSettings }) } })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    await userEvent.selectOptions(screen.getByLabelText('Card size'), '4 × 4')

    expect(screen.getByLabelText('Free space')).toBeDisabled()
    expect(
      screen.getByText('A 4 × 4 card has no centre square, so no free space.'),
    ).toBeInTheDocument()
  })

  it('sends a grouped set’s host to the set editor to change the card size', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot(
          {
            content: {
              name: 'Science',
              groupItems: true,
              board: [],
              squares: [],
              labels: [],
              playersMayDownload: false,
            },
          },
          { settings: hostSettings },
        ),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))

    expect(screen.getByLabelText('Card size')).toBeDisabled()
    expect(screen.getByRole('link', { name: 'set editor' })).toHaveAttribute(
      'href',
      `/host/${code}/set`,
    )
  })
})

describe('Your patterns (FR-21a)', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('draws a pattern on the card, and adds it only after the host confirms', async () => {
    const api = fakeApi({
      [snapshotPath]: { body: aHostSnapshot({}, { settings: hostSettings }) },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    await userEvent.click(screen.getByRole('button', { name: 'Draw a pattern' }))
    await userEvent.type(screen.getByLabelText('Pattern name'), 'Plus')
    for (const name of ['Row 1, column 3', 'Row 3, column 1', 'Row 3, column 5']) {
      await userEvent.click(screen.getByRole('button', { name }))
    }
    await userEvent.click(screen.getByRole('button', { name: 'Row 3, column 3 (free space)' }))
    await userEvent.click(screen.getByRole('button', { name: 'Add this pattern' }))

    expect(
      screen.getByText('Add “Plus”? You can rename it later, but not delete it.'),
    ).toBeInTheDocument()
    expect(api.sent.some((s) => s.path.endsWith('/patterns'))).toBe(false)
    await userEvent.click(screen.getByRole('button', { name: 'Add pattern' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/patterns'))?.body).toEqual({
        name: 'Plus',
        cells: [2, 10, 14, 12],
      })
    })
  })

  it('renames one of the host’s own patterns, since they can’t be deleted', async () => {
    const own = { id: 'custom-1', name: 'Plus', masks: [1], isLine: false, announcedAs: 'Plus' }
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ patterns: [...standardPatterns, own] }, { settings: hostSettings }),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Options' }))
    expect(screen.queryByRole('button', { name: /^Delete/ })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Rename Plus' }))
    const input = screen.getByLabelText('New name for Plus')
    await userEvent.clear(input)
    await userEvent.type(input, 'Do not use')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      const sent = api.sent.find((s) => s.method === 'PUT' && s.path.includes('/patterns/'))
      expect(sent?.path).toBe(`/api/sessions/${code}/patterns/custom-1`)
      expect(sent?.body).toEqual({ name: 'Do not use' })
    })
  })
})
