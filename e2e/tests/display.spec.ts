import { expect, test } from '@playwright/test'
import { grabBall, hostGame, startRound } from './game'

test('the display opened from the host console follows the calls', async ({ browser }) => {
  const { host, code } = await hostGame(browser)

  await host.getByRole('button', { name: 'Display', exact: true }).click()
  const [display] = await Promise.all([
    host.context().waitForEvent('page'),
    host.getByRole('button', { name: 'Open in a new tab' }).click(),
  ])
  await expect(display).toHaveURL(new RegExp(`/display/${code}`))
  await expect(display.getByRole('list', { name: 'Balls B' })).toBeVisible()
  await host.keyboard.press('Escape') // close the Displays dialog
  await expect(host.getByRole('dialog', { name: 'Displays' })).toBeHidden()

  await startRound(host)
  const ball = await grabBall(host)

  await expect(display.getByTestId('last-ball')).toContainText(String(ball))
})
