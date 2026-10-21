import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DisplayMenu } from './DisplayMenu'
import type { HostApi } from './hostApi'

const host = () =>
  ({
    displays: vi.fn().mockResolvedValue([]),
    setBoardSide: vi.fn().mockResolvedValue(undefined),
  }) as unknown as HostApi & { setBoardSide: ReturnType<typeof vi.fn> }

const content = {
  name: 'Science',
  groupItems: true,
  board: [],
  squares: [],
  labels: [],
  boardSide: 'Ball' as const,
  playersMayDownload: false,
}

describe('Displays: call board shows (FR-29)', () => {
  it('offers the choice only with a custom set, the ball side chosen', async () => {
    const { unmount } = render(<DisplayMenu host={host()} joinNote={null} onClose={vi.fn()} />)
    expect(await screen.findByRole('button', { name: 'Close' })).toBeInTheDocument()
    expect(screen.queryByRole('group', { name: 'Call board shows' })).not.toBeInTheDocument()
    unmount()

    render(<DisplayMenu host={host()} joinNote={null} content={content} onClose={vi.fn()} />)
    expect(await screen.findByRole('radio', { name: 'Balls (what is called)' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Squares (what players mark)' })).not.toBeChecked()
  })

  it('switches every board to the squares', async () => {
    const api = host()
    render(<DisplayMenu host={api} joinNote={null} content={content} onClose={vi.fn()} />)

    await userEvent.click(await screen.findByRole('radio', { name: 'Squares (what players mark)' }))

    expect(api.setBoardSide).toHaveBeenCalledWith('Square')
  })
})
