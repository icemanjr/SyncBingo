import { expect, type Browser, type Page } from '@playwright/test'

/** A phone-sized screen with touch, as most players use. */
export const phone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }
/** A laptop, as hosts often use. */
export const laptop = { viewport: { width: 1366, height: 860 } }

export const password = 'party'

/** The host creates a game from the landing page; returns their page and the game code. */
export async function hostGame(browser: Browser): Promise<{ host: Page; code: string }> {
  const host = await (await browser.newContext(laptop)).newPage()
  await host.goto('/')
  await host.getByRole('link', { name: 'Host a game' }).click()
  await host.getByLabel('Game password').fill(password)
  await host.getByRole('button', { name: 'Create game' }).click()
  await host.waitForURL(/\/host\/[a-z0-9]{16}$/)
  const code = host.url().split('/host/')[1] ?? ''
  return { host, code }
}

/**
 * A player joins on a phone, by the QR's link (code filled in) or by typing the code, and waits for
 * their card.
 */
export async function joinGame(
  browser: Browser,
  code: string,
  name: string,
  how: 'link' | 'typed' = 'link',
): Promise<Page> {
  const player = await (await browser.newContext(phone)).newPage()
  if (how === 'link') {
    await player.goto(`/join/${code}`)
  } else {
    await player.goto('/')
    await player.getByRole('link', { name: 'Join a game' }).click()
    await player.getByLabel('Game code').fill(code)
  }

  await player.getByLabel('Game password').fill(password)
  await player.getByLabel('Your name').fill(name)
  await player.getByRole('button', { name: 'Join' }).click()
  await expect(player.getByRole('grid', { name: 'Your card' })).toBeVisible()
  return player
}

/** The numbers on a player's card, read from the squares' accessible names ("B 7"); the free space is 0. */
export async function cardNumbers(player: Page): Promise<number[]> {
  const names = await player.getByRole('gridcell').evaluateAll((cells) =>
    cells.map((cell) => cell.getAttribute('aria-label') ?? ''),
  )
  return names.map((name) => Number(/(\d+)/.exec(name)?.[1] ?? 0))
}

/** The host starts the next round in manual mode and waits out the 10-second countdown. */
export async function startRound(host: Page): Promise<void> {
  await host.getByRole('button', { name: /^Start (Round \d+|New Round)$/ }).click()
  await expect(host.getByRole('button', { name: /^Grab ball/ })).toBeEnabled({ timeout: 20_000 })
}

/** The host calls one ball and returns its number, read from the Last Ball slot. */
export async function grabBall(host: Page): Promise<number> {
  const before = await host.getByTestId('last-ball').textContent()
  await host.getByRole('button', { name: 'Grab ball' }).click()
  await expect(host.getByTestId('last-ball')).not.toHaveText(before ?? '')
  const text = (await host.getByTestId('last-ball').textContent()) ?? ''
  return Number(/(\d+)/.exec(text)?.[1])
}
