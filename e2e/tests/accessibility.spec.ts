import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Browser, type Page } from '@playwright/test'
import { grabBall, hostGame, laptop, password, phone, startRound } from './game'

/**
 * Every screen, in light and dark, checked by axe-core against WCAG 2.1 A and AA (docs/06 §3): contrast,
 * names for every control, landmarks, headings, lists, ARIA use. A real game is played to reach each one.
 */
for (const colorScheme of ['light', 'dark'] as const) {
  test(`every screen passes WCAG 2.1 AA checks, ${colorScheme}`, async ({ browser }) => {
    test.setTimeout(120_000)
    const problems: string[] = []
    const scan = async (page: Page, screen: string) => {
      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze()
      for (const v of results.violations) {
        problems.push(
          `${screen}: ${v.id} (${v.impact ?? 'n/a'}) ${v.help}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`,
        )
      }
    }
    const open = async (b: Browser, size: typeof phone | typeof laptop, path = '/') => {
      const page = await (await b.newContext({ ...size, colorScheme })).newPage()
      await page.goto(path)
      return page
    }

    // Landing, then hosting a game.
    const host = await open(browser, laptop)
    await scan(host, 'landing (laptop)')
    await scan(await open(browser, phone), 'landing (phone)')
    await scan(await open(browser, phone, '/terms'), 'terms (phone)')
    await scan(await open(browser, phone, '/privacy'), 'privacy (phone)')
    await host.getByRole('link', { name: 'Host a game' }).click()
    await scan(host, 'create game')
    await host.getByLabel('Game password').fill(password)
    await host.getByRole('button', { name: 'Create game' }).click()
    await host.waitForURL(/\/host\/[a-z0-9]{16}$/)
    const code = host.url().split('/host/')[1] ?? ''

    // Joining, from the QR's link.
    const player = await open(browser, phone, `/join/${code}`)
    await scan(player, 'join (from the QR)')
    await player.getByLabel('Game password').fill(password)
    await player.getByLabel('Your name').fill('Ava')
    await player.getByRole('button', { name: 'Get my card' }).click()
    await expect(player.getByRole('grid', { name: 'Your card' })).toBeVisible()
    await scan(player, 'player, lobby')
    await expect(host.getByRole('region', { name: 'Players' }).getByText('Ava')).toBeVisible()
    await scan(host, 'host lobby')

    // The host's Options dialog.
    await host.getByRole('button', { name: 'Options' }).click()
    await scan(host, 'host Options dialog')
    await host.getByRole('button', { name: 'Close' }).click()

    // A round in progress, with a Bingo claim waiting.
    await startRound(host)
    for (let i = 0; i < 5; i++) await grabBall(host)
    await player.getByRole('button', { name: 'BINGO!' }).click()
    await expect(host.getByText(/Ava pressed Bingo/)).toBeVisible()
    await scan(host, 'host round (laptop)')
    const hostPhone = await (
      await browser.newContext({
        ...phone,
        colorScheme,
        storageState: await host.context().storageState(),
      })
    ).newPage()
    await hostPhone.goto(`/host/${code}`)
    await expect(hostPhone.getByRole('tab', { name: /Bingo!/ })).toBeVisible()
    await scan(hostPhone, 'host round (phone)')
    await scan(player, 'player round')
    await player.getByRole('button', { name: /patterns and current rules/ }).click()
    await scan(player, 'player patterns and rules')

    // The TV display.
    await host.getByRole('button', { name: 'Display', exact: true }).click()
    const [display] = await Promise.all([
      host.context().waitForEvent('page'),
      host.getByRole('button', { name: 'Open in a new tab' }).click(),
    ])
    await expect(display.getByTestId('last-ball')).toBeVisible()
    await display.setViewportSize({ width: 1280, height: 720 })
    await scan(display, 'TV display')

    expect(problems, problems.join('\n')).toEqual([])
  })
}

/**
 * Every color theme (FR-50a), in light and dark, on a player's round screen with called balls and a mark:
 * a player picks the theme on their own device, so one game covers them all.
 */
test('every color theme passes WCAG 2.1 AA checks, light and dark', async ({ browser }) => {
  test.setTimeout(180_000)
  const { host, code } = await hostGame(browser)
  const problems: string[] = []
  const players: Page[] = []
  for (const theme of ['Ocean', 'Forest', 'Halloween', 'Winter']) {
    for (const colorScheme of ['light', 'dark'] as const) {
      const context = await browser.newContext({ ...phone, colorScheme })
      await context.addInitScript((chosen) => {
        localStorage.setItem('syncbingo:palette', chosen)
      }, theme)
      const page = await context.newPage()
      await page.goto(`/join/${code}`)
      await page.getByLabel('Game password').fill(password)
      await page.getByLabel('Your name').fill(`${theme} ${colorScheme}`)
      await page.getByRole('button', { name: 'Get my card' }).click()
      await expect(page.getByRole('grid', { name: 'Your card' })).toBeVisible()
      players.push(page)
    }
  }

  await startRound(host)
  for (let i = 0; i < 3; i++) await grabBall(host)
  for (const page of players) {
    await page.getByRole('gridcell').first().click()
    await expect(page.locator('html')).toHaveAttribute('data-palette', /.+/)
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze()
    const who = await page.evaluate(
      () =>
        `${document.documentElement.dataset.palette ?? ''} ${document.documentElement.classList.contains('dark') ? 'dark' : 'light'}`,
    )
    for (const v of results.violations) {
      problems.push(
        `${who}: ${v.id} ${v.help}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`,
      )
    }
  }

  expect(problems, problems.join('\n')).toEqual([])
})

test('nothing animates for people who ask their device for reduced motion', async ({ browser }) => {
  const page = await (await browser.newContext({ ...phone, reducedMotion: 'reduce' })).newPage()
  await page.goto('/host/new')

  const transition = await page
    .getByRole('button', { name: 'Create game' })
    .evaluate((button) => getComputedStyle(button).transitionDuration)

  expect(Number.parseFloat(transition)).toBeLessThan(0.001) // seconds: 0.01 ms, i.e. off
})
