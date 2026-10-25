import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Card } from './Card'
import { defaultMarkStyle } from './markStyle'

function renderCard(columnLabels: string[] | null) {
  render(
    <Card
      card={{ cardId: 1, cells: [1, 16, 2, 17], marks: 0 }}
      columns={2}
      columnLabels={columnLabels}
      freeCell={null}
      freeText="FREE"
      isCalled={null}
      highlightMatches={false}
      markStyle={defaultMarkStyle}
      setFaces={[]}
      disabled={false}
      onToggle={() => undefined}
    />,
  )
}

describe("the card's column labels", () => {
  it("shows a letter above each column, and starts each square's name with it", () => {
    renderCard(['B', 'I'])

    expect(screen.getByText('B')).toBeInTheDocument()
    expect(screen.getByText('I')).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'B 2' })).toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'I 17' })).toBeInTheDocument()
  })

  it('shows no labels when the board has no groups for the columns', () => {
    renderCard(null)

    expect(screen.queryByText('B')).not.toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: '2' })).toBeInTheDocument()
  })
})
