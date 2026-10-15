import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { seats } from '../../storage/sessions'
import { aCall, aGame, aPlayerSnapshot } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { PlayScreen } from './PlayScreen'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`
const settings = aGame().settings

/** Normal Bingo's 12 lines in the server's order: 5 rows, 5 columns, 2 diagonals. */
const normalMasks = [
  ...Array.from({ length: 5 }, (_, r) => 0b11111 << (r * 5)),
  ...Array.from({ length: 5 }, (_, c) =>
    [0, 5, 10, 15, 20].reduce((m, o) => m | (1 << (o + c)), 0),
  ),
  [0, 6, 12, 18, 24].reduce((m, c) => m | (1 << c), 0),
  [4, 8, 12, 16, 20].reduce((m, c) => m | (1 << c), 0),
]

function renderPlay() {
  render(
    <MemoryRouter initialEntries={[`/play/${code}`]}>
      <Routes>
        <Route path="/play/:code" element={<PlayScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('the player extras', () => {
  beforeEach(() => {
    seats.save(code, { role: 'player', token: 'player-token', participantId: 'p1', name: 'Ava' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('opens patterns and current rules from the pattern name (FR-48)', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({
          state: 'Manual',
          round: 1,
          patterns: [{ id: 'normal', name: 'Normal Bingo', masks: normalMasks }],
          settings: { ...settings, freeMarking: false, freeSpace: 'Earned' },
        }),
      },
    })
    renderPlay()

    await userEvent.click(await screen.findByRole('button', { name: /Normal Bingo/ }))

    expect(
      screen.getByRole('img', { name: 'Any row, for example the top row' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('img', { name: 'Any column, for example column B' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Either diagonal' })).toBeInTheDocument()
    expect(screen.getByText(/must be earned first/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: 'Current rules' }))
    expect(screen.getByRole('tab', { name: 'Current rules' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByText('Only called numbers can be marked')).toBeInTheDocument()
    expect(screen.getByText('The free space must be earned')).toBeInTheDocument()
  })

  it('shows the call board when the host shares it', async () => {
    fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1, calls: [aCall(7)] }) },
    })
    renderPlay()

    await userEvent.click(await screen.findByRole('button', { name: 'Call board' }))

    expect(screen.getByRole('listitem', { name: 'B 7, called' })).toBeInTheDocument()
  })

  it('has no call board button when the host keeps it hidden', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({
          state: 'Manual',
          round: 1,
          settings: { ...settings, showCallBoard: false },
        }),
      },
    })
    renderPlay()

    await screen.findByRole('grid', { name: 'Your card' })
    expect(screen.queryByRole('button', { name: 'Call board' })).not.toBeInTheDocument()
  })

  it('switches auto-fill on when the host offers it', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({
          state: 'Manual',
          round: 1,
          settings: { ...settings, autoFillAvailable: true },
        }),
      },
      [`PUT /api/sessions/${code}/me/auto-fill`]: { body: { on: true } },
    })
    renderPlay()

    await userEvent.click(await screen.findByRole('checkbox', { name: 'Auto-fill missed numbers' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/me/auto-fill'))?.body).toEqual({ on: true })
    })
    expect(screen.getByRole('checkbox', { name: 'Auto-fill missed numbers' })).toBeChecked()
  })

  it('swaps the card between rounds when the host allows it', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({ settings: { ...settings, redrawPolicy: 'ParticipantsMayRedraw' } }),
      },
      [`POST /api/sessions/${code}/cards/redraw`]: { body: { cardId: 1, cells: [] } },
    })
    renderPlay()

    await userEvent.click(await screen.findByRole('button', { name: 'New card' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/cards/redraw'))?.body).toEqual({ cardId: 1 })
    })
  })

  it('shows the leaderboard when the host shows it, with you in bold', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({
          leaderboardVisible: true,
          leaderboard: [
            { participantId: 'p2', name: 'Ben', wins: 2 },
            { participantId: 'p1', name: 'Ava', wins: 1 },
          ],
        }),
      },
    })
    renderPlay()

    expect(await screen.findByRole('region', { name: 'Leaderboard' })).toHaveTextContent(
      '1. Ben2 wins2. Ava1 win',
    )
  })
})
