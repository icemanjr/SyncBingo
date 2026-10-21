import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { HostApi } from './hostApi'
import { SetCard } from './SetCard'

const content = {
  name: 'Science vocabulary',
  groupItems: true,
  board: [],
  squares: [],
  labels: ['B', 'I', 'N', 'G', 'O'],
  boardSide: 'Ball' as const,
  playersMayDownload: false,
}

describe("the host's custom set card (ADR 0009)", () => {
  it('names the set and keeps it private until the host shares it', async () => {
    const host = { setContentSharing: vi.fn().mockResolvedValue(undefined) }
    render(<SetCard code="abcd" token="t" content={content} host={host as unknown as HostApi} />)

    expect(screen.getByText('Science vocabulary')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Download this set' })).toBeInTheDocument()
    const share = screen.getByRole('checkbox', { name: /Players can download this set/ })
    expect(share).not.toBeChecked()

    await userEvent.click(share)

    expect(host.setContentSharing).toHaveBeenCalledWith(true)
  })
})
