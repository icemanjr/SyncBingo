import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { seats } from '../../storage/sessions'
import { aGame, aPlayerSnapshot } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { PlayScreen } from './PlayScreen'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`
const settings = aGame().settings

/** A player who joined during a round: no card yet (A3). */
function waitingSnapshot(state: 'Manual' | 'BetweenRounds') {
  const snapshot = aPlayerSnapshot({
    state,
    round: 1,
    settings: { ...settings, autoFillAvailable: true },
  })
  return { ...snapshot, you: snapshot.you && { ...snapshot.you, cards: [], waiting: true } }
}

function renderPlay() {
  render(
    <MemoryRouter initialEntries={[`/play/${code}`]}>
      <Routes>
        <Route path="/play/:code" element={<PlayScreen />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('the waiting room (A3)', () => {
  beforeEach(() => {
    seats.save(code, { role: 'player', token: 'player-token', participantId: 'p1', name: 'Ava' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('tells someone who joined mid-round they’ll be added, and lets them set up meanwhile', async () => {
    fakeApi({ [snapshotPath]: { body: waitingSnapshot('Manual') } })
    renderPlay()

    expect(await screen.findByRole('status', { name: 'Waiting to join' })).toHaveTextContent(
      'Round currently being played. You’ll be added as soon as it’s done.',
    )
    expect(screen.queryByRole('grid', { name: 'Your card' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /BINGO/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'My marks' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Auto-fill missed squares' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Call board' })).toBeInTheDocument()
  })

  it('says why if they are still waiting in the lobby', async () => {
    fakeApi({ [snapshotPath]: { body: waitingSnapshot('BetweenRounds') } })
    renderPlay()

    expect(
      await screen.findByText(
        'Waiting for a card: every different card is in use. The host can add entries to the set.',
      ),
    ).toBeInTheDocument()
  })
})
