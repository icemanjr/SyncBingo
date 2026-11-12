import { expect, test } from '@playwright/test'

// What search engines and ad reviewers read without running the app (docs/07, ADR 0012 amended).
test('content pages arrive as real HTML, each with its own title, and app routes get the empty shell', async ({
  request,
}) => {
  const faq = await (await request.get('/faq')).text()
  expect(faq).toContain('Can the host play too?')
  expect(faq).toContain('<title>Frequently asked questions · SyncBingo</title>')
  expect(faq).toContain('<link rel="canonical" href="https://syncbingo.com/faq"')

  expect(await (await request.get('/')).text()).toContain('What is SyncBingo?')
  expect(await (await request.get('/sets')).text()).toContain('Animal sounds')

  const shell = await (await request.get('/join/9ve4shp3k7mwa2qx')).text()
  expect(shell).toContain('<div id="root"></div>')

  const robots = await (await request.get('/robots.txt')).text()
  expect(robots).toContain('Sitemap: https://syncbingo.com/sitemap.xml')
  expect(robots).toContain('Disallow: /play/')
  expect(await (await request.get('/sitemap.xml')).text()).toContain(
    '<loc>https://syncbingo.com/how-to-play</loc>',
  )

  const set = await request.get('/sets/opposites.json')
  expect(set.ok()).toBe(true)
  expect(await set.json()).toMatchObject({ name: 'Opposites' })
})

test('a pre-built page comes alive: the theme switch and links work', async ({ page }) => {
  await page.goto('/how-to-play')
  await page.getByRole('radio', { name: 'Dark' }).click()
  await expect(page.locator('html')).toHaveClass(/dark/)

  await page.getByRole('link', { name: 'Ready-made sets', exact: true }).click() // the footer's link
  await expect(page.getByRole('heading', { level: 1, name: 'Ready-made sets' })).toBeVisible()
  await expect(page).toHaveTitle('Ready-made sets · SyncBingo')
})
