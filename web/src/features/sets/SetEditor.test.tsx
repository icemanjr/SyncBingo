import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { seats } from '../../storage/sessions'
import { aHostSnapshot } from '../../test/fixtures'
import { fakeApi } from '../../test/fakeApi'
import { SetEditor } from './SetEditor'

vi.mock('../../realtime/gameConnection', () => ({
  connectGame: vi.fn(() => Promise.resolve({ stop: () => Promise.resolve() })),
}))

const code = '9ve4shp3k7mwa2qx'
const snapshotPath = `GET /api/sessions/${code}/snapshot`
const sample = readFileSync(resolve(process.cwd(), '../samples/science-vocabulary.json'), 'utf8')

function renderEditor() {
  render(
    <MemoryRouter initialEntries={[`/host/${code}/set`]}>
      <Routes>
        <Route path="/host/:code/set" element={<SetEditor />} />
        <Route path="/host/:code" element={<p>The host console</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

const sampleFile = () => new File([sample], 'science.json', { type: 'application/json' })

describe('the set editor (FR-44)', () => {
  beforeEach(() => {
    seats.save(code, { role: 'host', token: 'host-token' })
  })

  afterEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('builds a word list entry by entry, saying what is still missing', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()

    const word = await screen.findByLabelText('Word or phrase')
    await userEvent.type(word, 'photosynthesis')
    await userEvent.click(screen.getByRole('button', { name: 'Add entry' }))
    await userEvent.type(word, 'PHOTOSYNTHESIS')
    await userEvent.click(screen.getByRole('button', { name: 'Add entry' }))

    expect(screen.getByRole('alert')).toHaveTextContent('That entry is already in the set.')
    expect(
      within(screen.getByRole('region', { name: 'Entries' })).getByText('photosynthesis'),
    ).toBeInTheDocument()
    expect(screen.getByText('A set needs 8–500 entries; this one has 1.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Use this set' })).toBeDisabled()
  })

  it('asks for what is called and what players mark once they no longer match', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()

    await userEvent.click(await screen.findByRole('checkbox', { name: 'Ball and square match' }))

    expect(screen.getByLabelText('What is called')).toBeInTheDocument()
    expect(screen.getByLabelText('What players mark')).toBeInTheDocument()
  })

  it('opens a set file in its groups, and uses it once the host takes responsibility', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()

    await userEvent.upload(await screen.findByLabelText('Open a set file'), sampleFile())

    expect(
      await screen.findByText(/Opened “Science vocabulary \(sample\)”: 75 entries/),
    ).toBeInTheDocument()
    const biology = screen.getByRole('region', { name: 'Group Biology' })
    expect(within(biology).getByDisplayValue('B')).toBeInTheDocument()
    expect(within(biology).getByText('Producer')).toBeInTheDocument()
    const use = screen.getByRole('button', { name: 'Use this set' })
    expect(use).toBeDisabled()

    await userEvent.click(screen.getByRole('checkbox', { name: /I’m responsible/ }))
    await userEvent.click(use)

    expect(await screen.findByText('The host console')).toBeInTheDocument()
    const put = api.sent.find((s) => s.method === 'PUT')
    expect(put?.path).toBe(`/api/sessions/${code}/content`)
    expect((put?.body as { groups: unknown[] }).groups).toHaveLength(5)
  })

  it('parks pairs as ungrouped while the host reorganises, then removes them all', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()
    await userEvent.upload(await screen.findByLabelText('Open a set file'), sampleFile())
    await screen.findByRole('region', { name: 'Group Biology' })

    await userEvent.type(screen.getByLabelText('Word or phrase'), 'comet')
    await userEvent.click(screen.getByRole('button', { name: 'Add entry' }))
    await userEvent.selectOptions(
      screen.getByLabelText(/^Group for An organism that makes its own food/),
      'Ungrouped',
    )

    const parked = screen.getByRole('region', { name: 'Ungrouped pairs' })
    expect(within(parked).getByRole('alert')).toHaveTextContent(
      'The set can’t be used until each of these is moved into a group or removed.',
    )
    expect(within(parked).getByText('Producer')).toBeInTheDocument()
    expect(
      screen.getByText('2 ungrouped pairs: move them into a group, or remove them.'),
    ).toBeInTheDocument()

    await userEvent.click(within(parked).getByRole('button', { name: 'Remove all' }))
    await userEvent.click(within(parked).getByRole('button', { name: 'Remove' }))

    expect(screen.queryByRole('region', { name: 'Ungrouped pairs' })).not.toBeInTheDocument()
    expect(
      within(screen.getByRole('region', { name: 'Group Biology' })).getByText('(14)'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/ungrouped pairs?:/)).not.toBeInTheDocument()
  })

  it('without Use groups, shows one list and sends no groups', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()
    await userEvent.upload(await screen.findByLabelText('Open a set file'), sampleFile())
    await screen.findByRole('region', { name: 'Group Biology' })

    await userEvent.click(screen.getByRole('checkbox', { name: /Use groups/ }))

    expect(screen.queryByRole('region', { name: 'Group Biology' })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Entries' })).toHaveTextContent('Producer')
    await userEvent.click(screen.getByRole('checkbox', { name: /I’m responsible/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Use this set' }))

    await screen.findByText('The host console')
    const put = api.sent.find((s) => s.method === 'PUT')
    expect((put?.body as { groups?: unknown }).groups).toBeUndefined()
  })

  it('fits the groups to a smaller card and sends the size with the set', async () => {
    const api = fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()
    await userEvent.upload(await screen.findByLabelText('Open a set file'), sampleFile())
    await screen.findByRole('region', { name: 'Group Biology' })

    await userEvent.selectOptions(screen.getByLabelText('Card size'), '3 × 3')

    expect(screen.queryByRole('region', { name: 'Group Geology & Earth' })).not.toBeInTheDocument()
    const parked = screen.getByRole('region', { name: 'Ungrouped pairs' })
    expect(within(parked).getByRole('heading')).toHaveTextContent('Ungrouped pairs (30)')
    await userEvent.click(within(parked).getByRole('button', { name: 'Remove all' }))
    await userEvent.click(within(parked).getByRole('button', { name: 'Remove' }))
    await userEvent.click(screen.getByRole('checkbox', { name: /I’m responsible/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Use this set' }))

    await screen.findByText('The host console')
    const put = api.sent.find((s) => s.method === 'PUT')
    expect(put?.query).toBe('?cardSize=ThreeByThree')
    expect((put?.body as { groups: unknown[] }).groups).toHaveLength(3)
  })

  it('warns when a small grouped set makes fewer cards than a full game', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()
    const tiny = JSON.stringify({
      format: 1,
      groups: [
        { name: 'a', pairs: [['a1'], ['a2'], ['a3']] },
        { name: 'b', pairs: [['b1'], ['b2']] },
        { name: 'c', pairs: [['c1'], ['c2'], ['c3']] },
      ],
    })
    await userEvent.upload(
      await screen.findByLabelText('Open a set file'),
      new File([tiny], 'tiny.json', { type: 'application/json' }),
    )
    await userEvent.selectOptions(screen.getByLabelText('Card size'), '3 × 3')

    expect(screen.getByText(/^72 possible cards\. Up to 72 players/)).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Word or phrase'), 'b3')
    await userEvent.selectOptions(screen.getByLabelText('Group'), 'b')
    await userEvent.click(screen.getByRole('button', { name: 'Add entry' }))
    expect(screen.getByText('216 possible cards.')).toBeInTheDocument()
  })

  it('renames a group once, for all its entries', async () => {
    fakeApi({ [snapshotPath]: { body: aHostSnapshot() } })
    renderEditor()
    await userEvent.upload(await screen.findByLabelText('Open a set file'), sampleFile())
    const biology = await screen.findByRole('region', { name: 'Group Biology' })

    await userEvent.click(within(biology).getByRole('button', { name: 'Rename' }))
    const name = within(biology).getByLabelText('Group name')
    await userEvent.clear(name)
    await userEvent.type(name, 'Life science')
    await userEvent.click(within(biology).getByRole('button', { name: 'Save' }))

    const renamed = screen.getByRole('region', { name: 'Group Life science' })
    expect(within(renamed).getByText('(15)')).toBeInTheDocument()
  })

  it('starts from the set the game uses', async () => {
    fakeApi({
      [snapshotPath]: {
        body: aHostSnapshot({
          content: {
            name: 'Science',
            groupItems: true,
            board: [],
            squares: [],
            labels: [],
            playersMayDownload: false,
          },
        }),
      },
      [`GET /api/sessions/${code}/content`]: { body: JSON.parse(sample) as unknown },
    })
    renderEditor()

    expect(await screen.findByRole('region', { name: 'Group Geology & Earth' })).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Back to numbers' })).toBeEnabled()
    })
  })
})
