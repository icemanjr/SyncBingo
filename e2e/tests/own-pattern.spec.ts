import { expect, test } from '@playwright/test'
import { hostGame, joinGame } from './game'

/**
 * The host's own pattern (FR-21a): drawn in Options, added after a confirmation, then chosen with Apply,
 * and the player is told what wins by its name.
 */
test('the host draws a plus sign and plays with it', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  const player = await joinGame(browser, code, 'Ava')

  await host.getByRole('button', { name: 'Options' }).first().click()
  await host.getByRole('button', { name: 'Draw a pattern' }).click()
  await host.getByLabel('Pattern name').fill('Plus sign')
  for (let i = 1; i <= 5; i++) {
    await host.getByRole('button', { name: new RegExp(`^Row 3, column ${String(i)}`) }).click()
    if (i !== 3) await host.getByRole('button', { name: `Row ${String(i)}, column 3` }).click()
  }
  await host.getByRole('button', { name: 'Add this pattern' }).click()
  await host.getByRole('button', { name: 'Add pattern' }).click()

  await host.getByRole('checkbox', { name: 'Plus sign' }).check()
  await host.getByRole('checkbox', { name: 'Normal Bingo' }).uncheck()
  await host.getByRole('button', { name: /^Apply/ }).click()
  await expect(host.getByRole('button', { name: 'Applied ✓' })).toBeVisible()

  await expect(player.getByText('Plus sign', { exact: true })).toBeVisible()
})
