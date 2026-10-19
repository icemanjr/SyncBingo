import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Card } from './Card'
import { defaultMarkStyle } from './markStyle'

/**
 * jsdom can't lay out or measure text, so these tests stand in a monospace measurer (60 px per character at
 * 100 px) and tiny 16 px squares: even "75" can't fit at Medium's 10 pt minimum, so its square is cut off with "…".
 */
describe('a cut-off square (FR-51)', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal(
      'ResizeObserver',
      class {
        readonly #callback: () => void
        constructor(callback: () => void) {
          this.#callback = callback
        }
        observe() {
          this.#callback()
        }
        disconnect() {
          // nothing to stop
        }
      },
    )
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      font: '',
      measureText: (text: string) => ({ width: text.length * 60 }),
    } as unknown as CanvasRenderingContext2D)
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(16)
    vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(16)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  function renderCard(onToggle = vi.fn()) {
    render(
      <Card
        card={{ cardId: 1, cells: [75, 74, 73, 72], marks: 0 }}
        columns={2}
        columnLabels={null}
        freeCell={null}
        called={null}
        highlightMatches={false}
        markStyle={defaultMarkStyle}
        setFaces={['75']}
        disabled={false}
        onToggle={onToggle}
      />,
    )
    return onToggle
  }

  it('ends with "…", keeps the full text for screen readers and on hover', () => {
    renderCard()

    const square = screen.getByRole('gridcell', { name: '75' })
    expect(square).toHaveTextContent('…')
    expect(square).toHaveAttribute('title', '75')
  })

  it('shows the full text on a long press, without marking the square', () => {
    const onToggle = renderCard()
    const square = screen.getByRole('gridcell', { name: '75' })

    fireEvent.pointerDown(square)
    act(() => {
      vi.advanceTimersByTime(500)
    })
    fireEvent.pointerUp(square)
    fireEvent.click(square)

    expect(screen.getByRole('tooltip')).toHaveTextContent('75')
    expect(onToggle).not.toHaveBeenCalled()
  })

  it('still marks on an ordinary tap', () => {
    const onToggle = renderCard()
    const square = screen.getByRole('gridcell', { name: '75' })

    fireEvent.pointerDown(square)
    fireEvent.pointerUp(square)
    fireEvent.click(square)

    expect(onToggle).toHaveBeenCalledWith(0, true)
  })
})
