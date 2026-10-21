import { expect, test } from '@playwright/test'
import { hostGame, joinGame, startRound } from './game'

/**
 * A custom set end to end (FR-44, ADR 0009): the host opens the sample science set in the editor and uses
 * it, a player's card shows its words under B-I-N-G-O, and a called ball reads out its clue.
 */
test('a host plays a game with a custom set', async ({ browser }) => {
  test.setTimeout(90_000)
  const { host, code } = await hostGame(browser)
  const player = await joinGame(browser, code, 'Ava')

  await host.getByRole('link', { name: 'Use a custom set' }).click()
  await host.waitForURL(`/host/${code}/set`)
  await host.getByLabel('Open a set file').setInputFiles('../samples/science-vocabulary.json')
  await expect(host.getByText(/Opened “Science vocabulary \(sample\)”: 75 entries/)).toBeVisible()
  await expect(host.getByRole('region', { name: 'Group Biology' })).toBeVisible()
  await host.getByRole('checkbox', { name: /I’m responsible/ }).check()
  await host.getByRole('button', { name: 'Use this set' }).click()

  // Back in the lobby with the set in use.
  await host.waitForURL(`/host/${code}`)
  await expect(host.getByRole('region', { name: 'Custom set' })).toContainText('Science vocabulary (sample)')

  // The player's new card: words, under the groups' short labels.
  const card = player.getByRole('grid', { name: 'Your card' })
  for (const label of ['B', 'I', 'N', 'G', 'O']) {
    await expect(card.getByText(label, { exact: true })).toBeVisible()
  }
  await expect(card.getByRole('gridcell', { name: /^B / }).first()).toBeVisible()

  await startRound(host)
  await host.getByRole('button', { name: 'Grab ball' }).click()
  // The last ball is a clue from the set, not a number.
  await expect(host.getByTestId('last-ball')).toHaveText(/[a-z]{4,}.*\./i)
  await expect(host.getByTestId('last-ball')).not.toHaveText(/^\s*[BINGO]?\s*\d+\s*$/)
})
