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
          settings: { ...settings, freeMarking: false, freeSpace: 'Earned' },
        }),
      },
    })
    renderPlay()

    await userEvent.click(await screen.findByRole('button', { name: /Normal Bingo/ }))

    expect(screen.getByRole('img', { name: 'Any of the 5 rows' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Any of the 5 columns' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Any of the 2 diagonals' })).toBeInTheDocument()
    expect(screen.getByText('Any one of these wins:')).toBeInTheDocument()
    expect(screen.getByText(/must be earned first/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: 'Current rules' }))
    expect(screen.getByRole('tab', { name: 'Current rules' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByText('Only called squares can be marked')).toBeInTheDocument()
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

    await userEvent.click(await screen.findByRole('checkbox', { name: 'Auto-fill missed squares' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/me/auto-fill'))?.body).toEqual({ on: true })
    })
    expect(screen.getByRole('checkbox', { name: 'Auto-fill missed squares' })).toBeChecked()
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

  it('shows the host’s color theme, unless the player picks their own (FR-50a)', async () => {
    fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ settings: { ...settings, theme: 'Ocean' } }) },
    })
    renderPlay()

    await screen.findByRole('grid', { name: 'Your card' })
    expect(document.documentElement.dataset.palette).toBe('Ocean')

    await userEvent.click(screen.getByRole('button', { name: 'My marks' }))
    const colors = screen.getByLabelText('Colors')
    expect(colors).toHaveDisplayValue('The game’s (Ocean)')
    await userEvent.selectOptions(colors, 'Halloween')
    expect(document.documentElement.dataset.palette).toBe('Halloween')
    expect(localStorage.getItem('syncbingo:palette')).toBe('Halloween')

    await userEvent.selectOptions(colors, 'The game’s (Ocean)')
    expect(document.documentElement.dataset.palette).toBe('Ocean')
    expect(localStorage.getItem('syncbingo:palette')).toBeNull()
  })
})
