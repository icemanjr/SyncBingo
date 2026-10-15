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
