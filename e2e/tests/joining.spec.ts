import { expect, test } from '@playwright/test'
import { cardNumbers, hostGame, joinGame, phone, startRound } from './game'

test('players join by the QR link and by typing the code, and the host sees them', async ({
  browser,
}) => {
  const { host, code } = await hostGame(browser)

  await joinGame(browser, code, 'Ava', 'link')
  await joinGame(browser, code, 'Ben', 'typed')

  const players = host.getByRole('region', { name: /Players/ })
  await expect(players.getByText('Ava')).toBeVisible()
  await expect(players.getByText('Ben')).toBeVisible()
})

test('someone joining mid-round waits, and gets a card when the round ends', async ({
  browser,
}) => {
  const { host, code } = await hostGame(browser)
  await joinGame(browser, code, 'Ava')
  await startRound(host)

  const late = await (await browser.newContext(phone)).newPage()
  await late.goto(`/join/${code}`)
  await expect(
    late.getByText('A round is in progress. Join now and you’ll be added as soon as it’s done.'),
  ).toBeVisible()
  await late.getByLabel('Game password').fill('party')
  await late.getByLabel('Your name').fill('Cara')
  await late.getByRole('button', { name: 'Get my card' }).click()

  // The waiting room (A3): no card yet, but the player can set up meanwhile.
  await expect(late).toHaveURL(new RegExp(`/play/${code}$`))
  await expect(late.getByRole('status', { name: 'Waiting to join' })).toBeVisible()
  await expect(late.getByRole('grid', { name: 'Your card' })).toHaveCount(0)
  await expect(late.getByRole('button', { name: 'My marks' })).toBeVisible()
  await expect(host.getByRole('region', { name: 'Waiting' })).toContainText('Cara')

  await host.getByRole('button', { name: 'Clear cards' }).click()
  await expect(late.getByRole('grid', { name: 'Your card' })).toBeVisible()
  await expect(host.getByRole('region', { name: 'Waiting' })).toHaveCount(0)
})

test('a phone that went to sleep comes back to the same card', async ({ browser }) => {
  const { code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  const before = await cardNumbers(ava)

  await ava.reload()

  await expect(ava.getByRole('grid', { name: 'Your card' })).toBeVisible()
  expect(await cardNumbers(ava)).toEqual(before)
})
