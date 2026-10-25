import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Card } from './Card'
import { defaultMarkStyle } from './markStyle'

function renderCard(freeText: string) {
  render(
    <Card
      card={{ cardId: 1, cells: [1, 16, 0, 17], marks: 0b0100 }}
      columns={2}
      columnLabels={null}
      freeCell={2}
      freeText={freeText}
      isCalled={null}
      highlightMatches={false}
      markStyle={defaultMarkStyle}
      setFaces={[]}
      disabled={false}
      onToggle={() => undefined}
    />,
  )
}

describe('the free space', () => {
  it('says FREE, and is read out as the free space', () => {
    renderCard('FREE')

    const free = screen.getByRole('gridcell', { name: 'Free space, marked' })
    expect(free).toHaveTextContent('FREE')
    expect(free).not.toHaveTextContent('★')
  })

  it('says what the set chose, and is read out with it', () => {
    renderCard('★ Week 5')

    expect(
      screen.getByRole('gridcell', { name: 'Free space: ★ Week 5, marked' }),
    ).toHaveTextContent('★ Week 5')
  })
})
