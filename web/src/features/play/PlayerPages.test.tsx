import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { GameConnectionOptions } from '../../realtime/gameConnection'
import { seats } from '../../storage/sessions'
import { aCall, aPlayerSnapshot } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { JoinPage } from './JoinPage'
import { PlayScreen } from './PlayScreen'

/** The connection the play screen opened, so a test can deliver events as the server would. */
let connection: GameConnectionOptions | undefined
vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn((options: GameConnectionOptions) => {
    connection = options
    return Promise.resolve({ stop: () => Promise.resolve() })
  }),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/join" element={<JoinPage />} />
        <Route path="/join/:code" element={<JoinPage />} />
        <Route path="/play/:code" element={<PlayScreen />} />
        <Route path="/" element={<p>home page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => {
  localStorage.clear()
  vi.unstubAllGlobals()
  connection = undefined
})

describe('joining', () => {
  it("lets a player change the code the QR found, in case it's the wrong game", async () => {
    fakeApi({})
    renderAt(`/join/${code}`)

    await userEvent.click(await screen.findByRole('button', { name: 'Change' }))

    expect(screen.getByLabelText('Game code')).toHaveValue('9ve4-shp3-k7mw-a2qx')
  })

  it('fills in the code from the QR link and joins', async () => {
    const api = fakeApi({
      [`GET /api/sessions/${code}/join-info`]: { body: { roundInProgress: false, round: 1 } },
      [`POST /api/sessions/${code}/join`]: {
        body: { participantId: 'p1', token: 'player-token', snapshot: aPlayerSnapshot() },
      },
      [snapshotPath]: { body: aPlayerSnapshot() },
    })
    renderAt(`/join/${code}`)

    expect(screen.getByText('Game found from the QR code')).toBeInTheDocument()
    expect(screen.getByText('9ve4-shp3-k7mw-a2qx')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Game password'), 'harvest')
    await userEvent.type(screen.getByLabelText('Your name'), 'Ava')
    await userEvent.click(screen.getByRole('button', { name: 'Get my card' }))

    expect(await screen.findByRole('grid', { name: 'Your card' })).toBeInTheDocument()
    expect(api.sent.find((s) => s.path.endsWith('/join'))?.body).toEqual({
      password: 'harvest',
      name: 'Ava',
    })
    expect(seats.load(code)).toEqual({
      role: 'player',
      token: 'player-token',
      participantId: 'p1',
      name: 'Ava',
    })
  })

  it('says when a round is in progress', async () => {
    fakeApi({
      [`GET /api/sessions/${code}/join-info`]: { body: { roundInProgress: true, round: 2 } },
    })
    renderAt(`/join/${code}`)

    expect(
      await screen.findByText(
        'A round is in progress. Join now and you’ll be added as soon as it’s done.',
      ),
    ).toBeInTheDocument()
  })

  it('sends a player who already joined back to their card', () => {
    seats.save(code, { role: 'player', token: 't', participantId: 'p1', name: 'Ava' })
    renderAt(`/join/${code}`)

    expect(screen.getByText("You're in this game as Ava.")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to my card' })).toHaveAttribute(
      'href',
      `/play/${code}`,
    )
  })
})

describe('the play screen', () => {
  beforeEach(() => {
    seats.save(code, { role: 'player', token: 'player-token', participantId: 'p1', name: 'Ava' })
  })

  it('marks a square and shows the mark the server confirms', async () => {
    const api = fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) },
      [`POST /api/sessions/${code}/marks`]: { body: { marks: (1 << 12) | 1 } },
    })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('gridcell', { name: 'B 1' }))

    expect(await screen.findByRole('gridcell', { name: 'B 1, marked' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(api.sent.find((s) => s.path.endsWith('/marks'))?.body).toEqual({
      cardId: 1,
      cell: 0,
      marked: true,
    })
    expect(api.sent.at(-1)?.headers.get('Authorization')).toBe('Bearer player-token')
  })

  it("shows the server's reason when a mark isn't allowed", async () => {
    fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) },
      [`POST /api/sessions/${code}/marks`]: {
        status: 422,
        body: { detail: 'Only called numbers can be marked in this game.', code: 'NotAllowed' },
      },
    })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('gridcell', { name: 'B 1' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Only called numbers can be marked in this game.',
    )
  })

  it('sends Bingo to the host and says so', async () => {
    const api = fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Auto', round: 1 }) },
      [`POST /api/sessions/${code}/claims`]: {
        status: 202,
        body: { claimedAt: '2026-10-14T19:00:00Z' },
      },
    })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('button', { name: 'BINGO!' }))

    expect(await screen.findByText('Sent to the host!')).toBeInTheDocument()
    expect(api.sent.find((s) => s.path.endsWith('/claims'))?.body).toEqual({ cardId: 1 })
  })

  it('has no Bingo button and a locked card in the lobby', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot() } })
    renderAt(`/play/${code}`)

    expect(await screen.findByText('Waiting for the host to start Round 1.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'BINGO!' })).not.toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'B 1' })).toBeDisabled()
  })

  it("shows the round's pattern while it counts down", async () => {
    fakeApi({
      [snapshotPath]: {
        body: aPlayerSnapshot({
          state: 'Starting',
          winCondition: 'blackout',
          countdownEndsAt: new Date(Date.now() + 8000).toISOString(),
        }),
      },
    })
    renderAt(`/play/${code}`)

    expect(await screen.findByText('This round: Blackout')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Blackout pattern' })).toBeInTheDocument()
  })

  it('leaves the game after a warning that mid-round a rejoin waits for the round to end', async () => {
    const api = fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) },
    })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('button', { name: 'More' }))
    await userEvent.click(screen.getByRole('button', { name: 'Leave game' }))
    expect(
      screen.getByText(
        'Leave the game? A round is in progress, so if you join again you’ll wait for it to end.',
      ),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Stay' }))
    expect(api.sent.some((s) => s.method === 'DELETE')).toBe(false)

    await userEvent.click(screen.getByRole('button', { name: 'More' }))
    await userEvent.click(screen.getByRole('button', { name: 'Leave game' }))
    await userEvent.click(screen.getByRole('button', { name: 'Leave' }))

    expect(await screen.findByText('home page')).toBeInTheDocument()
    expect(api.sent.some((s) => s.method === 'DELETE' && s.path.endsWith('/me'))).toBe(true)
    expect(seats.load(code)).toBeUndefined()
  })

  it('in the lobby, just asks "Leave the game?"', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot() } })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('button', { name: 'More' }))
    await userEvent.click(screen.getByRole('button', { name: 'Leave game' }))

    expect(screen.getByText('Leave the game?')).toBeInTheDocument()
  })

  it('tells everyone when someone leaves', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) } })
    renderAt(`/play/${code}`)
    await screen.findByRole('grid', { name: 'Your card' })

    act(() => {
      connection?.onEvent({ type: 'ParticipantLeft', seq: 1, data: { name: 'Ben' } })
    })

    expect(await screen.findByText('Ben left the game')).toBeInTheDocument()
  })

  it('says the game has ended when it expired, not that the host removed them', async () => {
    fakeApi({ [snapshotPath]: { status: 401, body: { title: 'Unauthorized', status: 401 } } })
    renderAt(`/play/${code}`)

    expect(await screen.findByRole('heading', { name: 'This game has ended' })).toBeInTheDocument()
    expect(screen.queryByText(/removed you/)).not.toBeInTheDocument()
    // Forgotten in an effect just after the page renders, so wait for it rather than race it.
    await waitFor(() => {
      expect(seats.load(code)).toBeUndefined()
    })
  })

  it('shows the last ball when the host shows it', async () => {
    fakeApi({
      [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1, calls: [aCall(37)] }) },
    })
    renderAt(`/play/${code}`)

    expect(await screen.findByTestId('last-ball')).toHaveTextContent('N 37')
  })

  it('names a shape win, as the server announced it', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) } })
    renderAt(`/play/${code}`)
    await screen.findByRole('grid', { name: 'Your card' })

    act(() => {
      connection?.onEvent({
        type: 'WinAwarded',
        seq: 1,
        data: { name: 'Ben', winCondition: 'rows+corners', wins: 1, announced: 'corners' },
      })
    })

    expect(await screen.findByText('Ben had Four Corners!')).toBeInTheDocument()
  })

  it('tells everyone who had Bingo, and celebrates with the winner', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) } })
    renderAt(`/play/${code}`)
    await screen.findByRole('grid', { name: 'Your card' })

    act(() => {
      connection?.onEvent({
        type: 'WinAwarded',
        seq: 1,
        data: { name: 'Ben', winCondition: 'normal', wins: 1 },
      })
      connection?.onEvent({
        type: 'WinAwarded',
        seq: 2,
        data: { name: 'Ava', winCondition: 'normal', wins: 1 },
      })
    })

    expect(screen.getByText('Ben had a Bingo!')).toBeInTheDocument()
    expect(screen.getByText('🎉 You have Bingo!')).toBeInTheDocument()
  })

  it('tells a removed player, and forgets the game on this device', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) } })
    renderAt(`/play/${code}`)
    await screen.findByRole('grid', { name: 'Your card' })

    act(() => {
      connection?.onEvent({ type: 'ParticipantRemoved', seq: 1, data: { name: 'Ava' } })
    })

    expect(screen.getByText('The host removed you from the game.')).toBeInTheDocument()
    await waitFor(() => {
      expect(seats.load(code)).toBeUndefined()
    })
  })

  it('keeps the chosen mark style and text size on this device', async () => {
    fakeApi({ [snapshotPath]: { body: aPlayerSnapshot({ state: 'Manual', round: 1 }) } })
    renderAt(`/play/${code}`)

    await userEvent.click(await screen.findByRole('button', { name: 'My marks' }))
    await userEvent.click(screen.getByRole('radio', { name: /Heart/ }))
    await userEvent.click(screen.getByRole('radio', { name: 'High contrast' }))

    await userEvent.click(screen.getByRole('radio', { name: 'Max' }))

    expect(JSON.parse(localStorage.getItem('syncbingo:mark-style') ?? '{}')).toMatchObject({
      shape: 'heart',
      colour: 'var(--ink)',
      textSize: 'max',
    })
    expect(screen.getByRole('gridcell', { name: 'B 1' }).querySelector('span')).toHaveClass(
      'text-4xl',
    )
  })
})
