import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { GameConnectionOptions } from '../../realtime/gameConnection'
import type { Paired, PairingHub } from '../../realtime/pairingConnection'
import { seats } from '../../storage/sessions'
import { aCall, aGame } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { DisplayView } from './DisplayView'
import { PairPage } from './PairPage'
import { TvPage } from './TvPage'

let connection: GameConnectionOptions | undefined
vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn((options: GameConnectionOptions) => {
    connection = options
    return Promise.resolve({ stop: () => Promise.resolve() })
  }),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`
const displaySnapshot = (game: Parameters<typeof aGame>[0] = {}) => ({
  game: aGame(game),
  you: null,
  host: null,
})

function renderDisplay() {
  render(
    <MemoryRouter initialEntries={[`/display/${code}`]}>
      <Routes>
        <Route path="/display/:code" element={<DisplayView />} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => {
  sessionStorage.clear()
  localStorage.clear()
  vi.unstubAllGlobals()
  connection = undefined
  window.history.replaceState(null, '', '/')
})

describe('the display view (FR-28)', () => {
  it('takes its token from the link, keeps it for this tab and clears it from the address bar', async () => {
    window.history.replaceState(null, '', `/display/${code}#t=display-token`)
    const api = fakeApi({ [snapshotPath]: { body: displaySnapshot() } })
    renderDisplay()

    await screen.findByRole('region', { name: 'How to join' })
    expect(window.location.hash).toBe('')
    expect(sessionStorage.getItem(`syncbingo:display:${code}`)).toBe('display-token')
    expect(api.sent[0]?.headers.get('Authorization')).toBe('Bearer display-token')
  })

  it('between rounds, shows the join QR and the password note as typed', async () => {
    sessionStorage.setItem(`syncbingo:display:${code}`, 'display-token')
    fakeApi({ [snapshotPath]: { body: displaySnapshot({ joinNote: 'Password: harvest' }) } })
    renderDisplay()

    const join = await screen.findByRole('region', { name: 'How to join' })
    expect(join).toHaveTextContent('Password: harvest')
    expect(join).toHaveTextContent('9ve4-shp3-k7mw-a2qx')
    expect(join).not.toHaveTextContent('Password note') // exactly what the host typed, no label
  })

  it('shows no note at all when the host has not written one', async () => {
    sessionStorage.setItem(`syncbingo:display:${code}`, 'display-token')
    fakeApi({ [snapshotPath]: { body: displaySnapshot({ joinNote: null }) } })
    renderDisplay()

    const join = await screen.findByRole('region', { name: 'How to join' })
    expect(join).not.toHaveTextContent(/note/i)
    expect(screen.queryByRole('button', { name: /Add|Edit/ })).not.toBeInTheDocument()
  })

  it('during a round, shows the last ball big and who had Bingo', async () => {
    sessionStorage.setItem(`syncbingo:display:${code}`, 'display-token')
    fakeApi({
      [snapshotPath]: {
        body: displaySnapshot({ state: 'Manual', round: 2, upcomingRound: 2, calls: [aCall(37)] }),
      },
    })
    renderDisplay()

    expect(await screen.findByTestId('last-ball')).toHaveTextContent('N 37')
    expect(screen.getByText('Round 2')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'How to join' })).not.toBeInTheDocument()

    act(() => {
      connection?.onEvent({
        type: 'WinAwarded',
        seq: 1,
        data: { name: 'Ava', winCondition: 'normal', wins: 1 },
      })
    })
    expect(screen.getByRole('status')).toHaveTextContent('Ava had a Bingo!')
  })

  it('stops when the host disconnects it, and forgets its token', async () => {
    sessionStorage.setItem(`syncbingo:display:${code}`, 'display-token')
    fakeApi({ [snapshotPath]: { body: displaySnapshot() } })
    renderDisplay()
    await screen.findByRole('region', { name: 'How to join' })

    act(() => {
      connection?.onNotice({ type: 'DisplayDisconnected', data: {} })
    })

    expect(screen.getByText('The host disconnected this display.')).toBeInTheDocument()
    await waitFor(() => {
      expect(sessionStorage.getItem(`syncbingo:display:${code}`)).toBeNull()
    })
  })

  it('says the game has ended when it expired, not that the host disconnected it', async () => {
    sessionStorage.setItem(`syncbingo:display:${code}`, 'display-token')
    fakeApi({ [snapshotPath]: { status: 401, body: { title: 'Unauthorized', status: 401 } } })
    renderDisplay()

    expect(await screen.findByText('This game has ended. Thanks for playing!')).toBeInTheDocument()
    expect(screen.queryByText(/disconnected/)).not.toBeInTheDocument()
    await waitFor(() => {
      expect(sessionStorage.getItem(`syncbingo:display:${code}`)).toBeNull()
    })
  })

  it('without a token, says how to open it', () => {
    renderDisplay()

    expect(screen.getByText(/Open display/)).toBeInTheDocument()
  })
})

/** A pairing hub the test answers for. */
function fakePairingHub() {
  const handlers: { paired?: (result: Paired) => void; expired?: () => void } = {}
  const hub: PairingHub = {
    on: (method: string, handler: never) => {
      if (method === 'Paired') handlers.paired = handler
      else handlers.expired = handler
    },
    start: () => Promise.resolve(),
    stop: () => Promise.resolve(),
  }
  return { hub, handlers }
}

describe('connecting a TV (ADR 0010)', () => {
  const pairing = {
    pairingId: 'p'.repeat(64),
    pairingCode: 'K7M-2QX',
    expiresAt: new Date(Date.now() + 600_000).toISOString(),
  }

  it('shows a code and becomes the display once the host approves it', async () => {
    fakeApi({ 'POST /api/tv/pairings': { status: 201, body: pairing } })
    const { hub, handlers } = fakePairingHub()
    render(
      <MemoryRouter initialEntries={['/tv']}>
        <Routes>
          <Route path="/tv" element={<TvPage buildHub={() => hub} />} />
          <Route path="/display/:code" element={<p>display for game</p>} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('K7M-2QX')).toBeInTheDocument()
    // The page subscribes in an effect after the code shows; on a slow machine that comes a little later.
    await waitFor(() => {
      expect(handlers.paired).toBeDefined()
    })
    act(() => {
      handlers.paired?.({ sessionCode: code, displayToken: 'tv-token' })
    })

    expect(await screen.findByText('display for game')).toBeInTheDocument()
  })

  it('offers a new code when this one expires', async () => {
    fakeApi({ 'POST /api/tv/pairings': { status: 201, body: pairing } })
    const { hub, handlers } = fakePairingHub()
    render(
      <MemoryRouter>
        <TvPage buildHub={() => hub} />
      </MemoryRouter>,
    )

    await screen.findByText('K7M-2QX')
    await waitFor(() => {
      expect(handlers.expired).toBeDefined()
    })
    act(() => {
      handlers.expired?.()
    })

    expect(await screen.findByText('That code expired.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Get a new code' })).toBeInTheDocument()
  })
})

describe("the TV's QR, opened on the host's phone", () => {
  function renderPair() {
    render(
      <MemoryRouter initialEntries={['/pair/K7M2QX']}>
        <Routes>
          <Route path="/pair/:pairingCode" element={<PairPage />} />
        </Routes>
      </MemoryRouter>,
    )
  }

  it('connects the TV to the game this browser hosts', async () => {
    seats.save(code, { role: 'host', token: 'host-token' })
    const api = fakeApi({})
    renderPair()

    await userEvent.click(
      screen.getByRole('button', { name: 'Show game 9ve4-shp3-k7mw-a2qx on the TV' }),
    )

    expect(await screen.findByText('Connected! The TV is showing the game.')).toBeInTheDocument()
    expect(api.sent[0]?.body).toEqual({ pairingCode: 'K7M2QX' })
  })

  it('sends someone who is not hosting to host a game', () => {
    renderPair()

    expect(screen.getByRole('link', { name: 'Host a game' })).toBeInTheDocument()
  })
})
