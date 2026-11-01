import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Schemas } from '../../api/client'
import { seats } from '../../storage/sessions'
import { fakeApi } from '../../test/fakeApi'
import { aHostSnapshot, standardPatterns } from '../../test/fixtures'
import { HostConsole } from '../host/HostConsole'
import { csvField, summaries, summaryCsv, totals, type GameSummary } from './summary'
import { SummaryPage } from './SummaryPage'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'

const win = (name: string, changes: Partial<Schemas['WinRecord']> = {}): Schemas['WinRecord'] => ({
  round: 1,
  participantId: name.toLowerCase(),
  name,
  cardId: 1,
  winConditionId: 'rows+columns+diagonals',
  awardedAt: '2026-10-31T19:10:00Z',
  source: 'Claimed',
  wins: 1,
  announced: null,
  ...changes,
})

const rounds: Schemas['RoundSummary'][] = [
  {
    number: 1,
    startedAt: '2026-10-31T19:00:00Z',
    endedAt: '2026-10-31T19:12:00Z',
    winConditionsUsed: ['rows+columns+diagonals', 'blackout'],
    wins: [win('Ava'), win('Ben', { source: 'Manual', winConditionId: 'blackout' })],
    contentName: null,
    ballsCalled: 31,
  },
  {
    number: 2,
    startedAt: '2026-10-31T19:15:00Z',
    endedAt: '2026-10-31T19:20:00Z',
    winConditionsUsed: ['corners'],
    wins: [],
    contentName: 'Week 5 words',
    ballsCalled: 12,
  },
]

const summary: GameSummary = {
  code,
  endedAt: '2026-10-31T19:21:00Z',
  players: 6,
  leaderboard: [
    { participantId: 'ava', name: 'Ava', wins: 1 },
    { participantId: 'ben', name: 'Ben', wins: 1 },
  ],
  rounds,
  patterns: {
    patterns: standardPatterns,
    presets: [{ id: 'normal', name: 'Normal Bingo', patternIds: ['rows', 'columns', 'diagonals'] }],
  },
}

afterEach(() => {
  sessionStorage.clear()
  localStorage.clear()
  vi.unstubAllGlobals()
})

describe('the game summary (FR-40a)', () => {
  it('adds up the players, rounds, balls and wins', () => {
    expect(totals(summary)).toEqual({ players: 6, rounds: 2, balls: 43, wins: 2 })
  })

  it('exports one row per win, and a row for a round nobody won', () => {
    const lines = summaryCsv(summary).trimEnd().split('\r\n')

    expect(lines[0]).toBe('Round,Started,Ended,Balls called,Patterns,Set,Winner,Won with,How,Wins')
    expect(lines).toHaveLength(4)
    expect(lines[1]).toContain(',"Normal Bingo, then Blackout",Numbers,Ava,Bingo,Pressed Bingo,1')
    expect(lines[2]).toContain(',Ben,Bingo,Awarded by the host,1')
    expect(lines[3]).toContain(',Four Corners,Week 5 words,,,,')
  })

  it('never lets a name run as a spreadsheet formula', () => {
    expect(csvField('=HYPERLINK("x")')).toBe(`"'=HYPERLINK(""x"")"`)
    expect(csvField('+1')).toBe("'+1")
    expect(csvField('-2')).toBe("'-2")
    expect(csvField('@sum')).toBe("'@sum")
    expect(csvField('Ava')).toBe('Ava')
  })

  it('shows the totals, the final results and every round, with print and CSV', () => {
    summaries.save(summary)
    render(
      <MemoryRouter initialEntries={[`/host/${code}/summary`]}>
        <Routes>
          <Route path="/host/:code/summary" element={<SummaryPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { level: 1, name: 'Game summary' })).toBeInTheDocument()
    expect(screen.getByText('Balls called').nextSibling).toHaveTextContent('43')
    expect(screen.getByRole('heading', { name: 'Final results' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Round 1' })).toHaveTextContent(
      'Normal Bingo, then Blackout',
    )
    expect(screen.getByRole('region', { name: 'Round 2' })).toHaveTextContent('No winners.')
    expect(screen.getByRole('button', { name: 'Print or save as PDF' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Export as CSV' })).toBeInTheDocument()
  })

  it('says so when this tab holds no summary', () => {
    render(
      <MemoryRouter initialEntries={[`/host/${code}/summary`]}>
        <Routes>
          <Route path="/host/:code/summary" element={<SummaryPage />} />
        </Routes>
      </MemoryRouter>,
    )

    expect(screen.getByText(/No summary is saved for this game/)).toBeInTheDocument()
  })

  it('ending the game keeps the summary on this device and opens it', async () => {
    seats.save(code, { role: 'host', token: 'host-token' })
    const api = fakeApi({
      [`GET /api/sessions/${code}/snapshot`]: { body: aHostSnapshot() },
      [`GET /api/sessions/${code}/leaderboard`]: {
        body: { entries: summary.leaderboard, rounds },
      },
    })
    render(
      <MemoryRouter initialEntries={[`/host/${code}`]}>
        <Routes>
          <Route path="/host/:code" element={<HostConsole />} />
          <Route path="/host/:code/summary" element={<SummaryPage />} />
        </Routes>
      </MemoryRouter>,
    )

    await userEvent.click(await screen.findByRole('button', { name: /More: leaderboard/ }))
    await userEvent.click(screen.getByRole('button', { name: 'End game…' }))
    await userEvent.click(screen.getByRole('button', { name: 'End game' }))

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Game summary' }),
    ).toBeInTheDocument()
    expect(api.sent.map((s) => `${s.method} ${s.path}`)).toEqual(
      expect.arrayContaining([`POST /api/sessions/${code}/end`]),
    )
    expect(summaries.load(code)?.rounds).toHaveLength(2)
  })
})
