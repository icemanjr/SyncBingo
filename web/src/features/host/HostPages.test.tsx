import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { seats } from '../../storage/sessions'
import { aCall, aHostSnapshot } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { CreateGamePage } from './CreateGamePage'
import { HostConsole } from './HostConsole'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`

function renderConsole() {
  render(
    <MemoryRouter initialEntries={[`/host/${code}`]}>
      <Routes>
        <Route path="/host/:code" element={<HostConsole />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('creating a game', () => {
  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('creates the game, keeps the host token and opens the console', async () => {
    const api = fakeApi({
      'POST /api/sessions': {
        status: 201,
        body: {
          code: '9ve4-shp3-k7mw-a2qx',
          hostToken: 'host-token',
          joinUrl: 'x',
          snapshot: aHostSnapshot(),
        },
      },
    })
    render(
      <MemoryRouter initialEntries={['/host/new']}>
        <Routes>
          <Route path="/host/new" element={<CreateGamePage />} />
          <Route path="/host/:code" element={<p>console for {code}</p>} />
        </Routes>
      </MemoryRouter>,
    )

    await userEvent.type(screen.getByLabelText('Game password'), 'harvest')
    await userEvent.click(screen.getByRole('button', { name: 'Create game' }))

    expect(await screen.findByText(`console for ${code}`)).toBeInTheDocument()
    expect(api.sent[0]?.body).toMatchObject({ password: 'harvest' })
    expect(seats.load(code)).toEqual({ role: 'host', token: 'host-token' })
  })

  it("shows the server's reason when it refuses", async () => {
    fakeApi({
      'POST /api/sessions': {
        status: 429,
        body: {
          detail: 'Too many attempts. Please wait a moment and try again.',
          code: 'TooManyRequests',
        },
      },
    })
    render(
      <MemoryRouter>
        <CreateGamePage />
      </MemoryRouter>,
    )

    await userEvent.type(screen.getByLabelText('Game password'), 'harvest')
    await userEvent.click(screen.getByRole('button', { name: 'Create game' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many attempts. Please wait a moment and try again.',
    )
  })
})

describe('the host console', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('in the lobby, deals everyone new cards with one button', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Deal New Cards' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/cards/clear'))?.body).toEqual({
        redrawCards: true,
      })
    })
    expect(await screen.findByRole('button', { name: 'Dealt ✓' })).toBeDisabled()
    expect(screen.queryByRole('checkbox', { name: /new cards/i })).not.toBeInTheDocument()
  })

  it('offers Deal New Cards only in the lobby, and Clear cards keeps the cards', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot({ state: 'Manual', round: 1 }) } })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Clear cards' }))
    expect(screen.queryByRole('button', { name: 'Deal New Cards' })).not.toBeInTheDocument()
    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/cards/clear'))?.body).toEqual({
        redrawCards: false,
      })
    })
  })

  it('in the lobby, starts Round 1 in the chosen mode', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderConsole()

    await userEvent.click(await screen.findByRole('radio', { name: 'Auto' }))
    await userEvent.click(screen.getByRole('button', { name: 'Start Round 1' }))

    await waitFor(() => {
      expect(api.sent.find((s) => s.path.endsWith('/rounds/countdown'))?.body).toEqual({
        startInAuto: true,
      })
    })
    expect(api.sent.at(-1)?.headers.get('Authorization')).toBe('Bearer host-token')
  })

  it('in manual mode, grabs balls and shows the last one', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Manual', round: 1, calls: [aCall(7), aCall(37, 2)] }),
      },
    })
    renderConsole()

    expect(await screen.findByTestId('last-ball')).toHaveTextContent('N 37')
    await userEvent.click(screen.getByRole('button', { name: 'Grab ball' }))

    await waitFor(() => {
      expect(api.sent.some((s) => s.method === 'POST' && s.path.endsWith('/balls'))).toBe(true)
    })
  })

  it('in auto mode, offers only Pause for calling', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot({ state: 'Auto', round: 1 }) } })
    renderConsole()

    expect(await screen.findByRole('button', { name: 'Pause' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Grab ball' })).not.toBeInTheDocument()
  })

  it('waits out the reading time after a rules change before the next ball', async () => {
    const soon = new Date(Date.now() + 3000).toISOString()
    fakeApi({
      [snapshotPath]: { body: aHostSnapshot({ state: 'Manual', round: 1, nextBallFrom: soon }) },
    })
    renderConsole()

    expect(await screen.findByRole('button', { name: /Grab ball \d…/ })).toBeDisabled()
  })

  it("awards a claim with the claim's id, so a double tap counts once", async () => {
    const claim = {
      id: 4,
      round: 1,
      participantId: 'p1',
      name: 'Ava',
      cardId: 1,
      winConditionId: 'normal',
      claimedAt: '2026-10-14T19:00:00Z',
      matches: 1,
      alreadyAwarded: 0,
      awardable: 1,
      status: 'Pending' as const,
    }
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({ state: 'Manual', round: 1 }, { pendingClaims: [claim] }),
      },
    })
    renderConsole()

    expect(await screen.findByText(/Ava pressed Bingo/)).toHaveTextContent('✓ 1 line')
    await userEvent.click(screen.getByRole('button', { name: 'Award' }))

    await waitFor(() => {
      const award = api.sent.find((s) => s.path.endsWith('/wins'))
      expect(award?.body).toMatchObject({ participantId: 'p1', claimId: 4 })
      expect(award?.headers.get('Idempotency-Key')).toBeTruthy()
    })
  })

  it('asks twice before removing a player', async () => {
    const api = fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot(
          {},
          {
            participants: [{ participantId: 'p1', name: 'Ava', joinedAt: '2026-10-14T19:00:00Z' }],
          },
        ),
      },
    })
    renderConsole()

    await userEvent.click(await screen.findByRole('button', { name: 'Remove' }))
    expect(api.sent.some((s) => s.method === 'DELETE')).toBe(false)
    await userEvent.click(screen.getByRole('button', { name: 'Remove Ava?' }))

    await waitFor(() => {
      expect(
        api.sent.some((s) => s.method === 'DELETE' && s.path.endsWith('/participants/p1')),
      ).toBe(true)
    })
  })

  it('tells a browser that is not the host', () => {
    localStorage.clear()
    renderConsole()

    expect(screen.getByText("This browser isn't hosting that game.")).toBeInTheDocument()
  })
})
