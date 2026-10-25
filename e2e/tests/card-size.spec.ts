import { expect, test } from '@playwright/test'
import { hostGame, joinGame } from './game'

/**
 * Card sizes (FR-26a): the host picks a 3 × 3 card in Options, and the player is dealt one, B I N over
 * balls 1–45 with a free centre. The letter patterns go, since they need a 5 × 5 card.
 */
test('the host switches to a 3 × 3 card and everyone gets one', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  const player = await joinGame(browser, code, 'Ava')

  await host.getByRole('button', { name: 'Options' }).first().click()
  await host.getByLabel('Card size').selectOption('3 × 3')
  await expect(host.getByRole('checkbox', { name: 'Letter B' })).toHaveCount(0)
  await host.getByRole('button', { name: /^Apply/ }).click()
  await expect(host.getByRole('button', { name: 'Applied ✓' })).toBeVisible()

  const card = player.getByRole('grid', { name: 'Your card' })
  await expect(card.getByRole('gridcell')).toHaveCount(9)
  for (const label of ['B', 'I', 'N']) {
    await expect(card.getByText(label, { exact: true })).toBeVisible()
  }
  await expect(card.getByText('G', { exact: true })).toHaveCount(0)
})
