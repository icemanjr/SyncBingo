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

test('joining is refused during a round, and works again between rounds', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  await joinGame(browser, code, 'Ava')
  await startRound(host)

  const late = await (await browser.newContext(phone)).newPage()
  await late.goto(`/join/${code}`)
  await late.getByLabel('Game password').fill('party')
  await late.getByLabel('Your name').fill('Cara')
  // The page warns as soon as it loads; what matters is the server's answer to the press itself.
  await expect(late.getByText('A round is in progress. You can join between rounds.')).toBeVisible()
  const [refused] = await Promise.all([
    late.waitForResponse((r) => r.url().endsWith(`/api/sessions/${code}/join`)),
    late.getByRole('button', { name: 'Get my card' }).click(),
  ])
  expect(refused.status()).toBe(409)

  await host.getByRole('button', { name: 'Clear cards' }).click()
  await expect(host.getByRole('button', { name: 'Start Round 2' })).toBeVisible() // back in the lobby
  await late.getByRole('button', { name: 'Get my card' }).click()
  await expect(late).toHaveURL(new RegExp(`/play/${code}$`))
  await expect(late.getByRole('grid', { name: 'Your card' })).toBeVisible()
})

test('a phone that went to sleep comes back to the same card', async ({ browser }) => {
  const { code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  const before = await cardNumbers(ava)

  await ava.reload()

  await expect(ava.getByRole('grid', { name: 'Your card' })).toBeVisible()
  expect(await cardNumbers(ava)).toEqual(before)
})
