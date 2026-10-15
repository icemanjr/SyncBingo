import { expect, test } from '@playwright/test'
import { cardNumbers, grabBall, hostGame, joinGame, startRound } from './game'

/** Every straight line on a 5×5 card, as cell indexes; the free centre (12) always counts. */
const lines = [
  ...[0, 1, 2, 3, 4].map((row) => [0, 1, 2, 3, 4].map((col) => row * 5 + col)),
  ...[0, 1, 2, 3, 4].map((col) => [0, 1, 2, 3, 4].map((row) => row * 5 + col)),
  [0, 6, 12, 18, 24],
  [4, 8, 12, 16, 20],
]
const hasLine = (card: number[], called: Set<number>) =>
  lines.some((line) => line.every((cell) => cell === 12 || called.has(card[cell] ?? -1)))

test('the host calls balls, a player marks one, and a real Bingo is claimed, awarded and announced', async ({
  browser,
}) => {
  const { host, code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  const ben = await joinGame(browser, code, 'Ben')
  const cards = { Ava: await cardNumbers(ava), Ben: await cardNumbers(ben) }
  await startRound(host)

  // Call until someone has a line, marking Ava's first called number on the way.
  const called = new Set<number>()
  let marked = false
  let winner: 'Ava' | 'Ben' | undefined
  while (!winner) {
    const ball = await grabBall(host)
    called.add(ball)
    if (!marked && cards.Ava.includes(ball)) {
      const square = ava.getByRole('gridcell', { name: new RegExp(` ${String(ball)}$`) })
      await square.click()
      await expect(ava.getByRole('gridcell', { name: new RegExp(` ${String(ball)}, marked$`) })).toBeVisible()
      marked = true
    }
    winner = hasLine(cards.Ava, called) ? 'Ava' : hasLine(cards.Ben, called) ? 'Ben' : undefined
  }

  const [winnerPage, otherPage, other] = winner === 'Ava' ? [ava, ben, 'Ben'] : [ben, ava, 'Ava']
  await winnerPage.getByRole('button', { name: 'BINGO!' }).click()
  await expect(winnerPage.getByText('Sent to the host!')).toBeVisible()

  // Win detection (on by default) has already told the host; the claim is what gets awarded.
  await expect(host.getByText(new RegExp(`Detected: ${winner} has`))).toBeVisible()
  const claim = host.getByRole('listitem').filter({ hasText: `${winner} pressed Bingo` })
  await expect(claim).toContainText(/✓ \d+ lines?/)
  await claim.getByRole('button', { name: 'Award', exact: true }).click()

  await expect(winnerPage.getByText('🎉 You have Bingo!')).toBeVisible()
  await expect(otherPage.getByText(`${winner} had a Bingo!`)).toBeVisible()
  expect(other).not.toBe(winner)

  await host.getByRole('button', { name: 'Leaderboard', exact: true }).click()
  const leaderboard = host.getByRole('dialog', { name: 'Leaderboard' })
  await expect(leaderboard.getByRole('listitem').filter({ hasText: winner })).toContainText('1 win')
})

test('a Bingo that is not on the called numbers is awarded only after the host confirms', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  await startRound(host)

  await ava.getByRole('button', { name: 'BINGO!' }).click()
  await expect(host.getByText(/Ava pressed Bingo · ✗ not on called numbers/)).toBeVisible()
  await host.getByRole('button', { name: 'Award', exact: true }).click()
  await expect(host.getByText("Award a Bingo that isn't on the called numbers?")).toBeVisible()
  await host.getByRole('button', { name: 'Award anyway' }).click()

  await expect(ava.getByText('🎉 You have Bingo!')).toBeVisible()
})

test('changing the pattern mid-round tells the players what changed', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  await startRound(host)
  await grabBall(host)

  await host.getByRole('button', { name: 'Options' }).click()
  await host.getByRole('checkbox', { name: 'Blackout' }).check()
  await host.getByRole('checkbox', { name: 'Normal Bingo' }).uncheck()
  await host.getByRole('button', { name: /^Apply/ }).click()

  const notice = ava.getByRole('region', { name: 'Rules updated' })
  await expect(notice).toContainText('Pattern changed to Blackout')
  await expect(ava.getByRole('button', { name: /^Blackout/ })).toBeVisible()
})

test('a player can leave, and everyone hears about it', async ({ browser }) => {
  const { host, code } = await hostGame(browser)
  const ava = await joinGame(browser, code, 'Ava')
  const ben = await joinGame(browser, code, 'Ben')
  await startRound(host)

  await ava.getByRole('button', { name: 'Leave game' }).click()
  await expect(ava.getByText(/A round is in progress, so you can only join again between rounds/)).toBeVisible()
  await ava.getByRole('button', { name: 'Leave', exact: true }).click()

  await expect(ava).toHaveURL(/\/$/)
  await expect(ben.getByText('Ava left the game')).toBeVisible()
  await expect(host.getByText('Ava left the game')).toBeVisible()
})
