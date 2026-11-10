// Renders the content pages to HTML after the build (docs/07), and writes robots.txt and sitemap.xml.
// dist/index.html becomes the pre-built landing page; the empty shell every other route loads is kept as
// dist/app.html (Caddy, nginx and CloudFront serve it for the app's routes).
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const web = join(dirname(fileURLToPath(import.meta.url)), '..')
const dist = join(web, 'dist')
const { render, prebuiltPages } = await import(join(web, 'dist-ssr', 'prerender.js'))

const origin = (process.env.VITE_SITE_ORIGIN ?? readEnv('VITE_SITE_ORIGIN')).replace(/\/$/, '')
const shell = readFileSync(join(dist, 'index.html'), 'utf8')
writeFileSync(join(dist, 'app.html'), shell)

const escape = (text) =>
  text.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

for (const page of prebuiltPages) {
  const html = shell
    .replace('<div id="root"></div>', `<div id="root">${render(page.path)}</div>`)
    .replace(/<title>[^<]*<\/title>/, `<title>${escape(page.title)}</title>`)
    .replace(
      /<meta\s+name="description"\s+content="[^"]*"\s*\/?>/,
      `<meta name="description" content="${escape(page.description)}" />`,
    )
    .replace('</head>', `  <link rel="canonical" href="${origin}${page.path}" />\n  </head>`)
  if (!html.includes(`<link rel="canonical"`) || html.includes('<div id="root"></div>'))
    throw new Error(`Could not pre-build ${page.path}: the page template changed.`)
  const file = page.path === '/' ? join(dist, 'index.html') : join(dist, page.path, 'index.html')
  mkdirSync(dirname(file), { recursive: true })
  writeFileSync(file, html)
}

// Search engines may read the content pages; game screens need a code and a token, so there is nothing
// for them there. Google's ad crawler may read everything, as AdSense asks.
writeFileSync(
  join(dist, 'robots.txt'),
  `User-agent: *
Disallow: /host/
Disallow: /play/
Disallow: /join/
Disallow: /display/
Disallow: /pair/
Disallow: /api/

User-agent: Mediapartners-Google
Allow: /

Sitemap: ${origin}/sitemap.xml
`,
)

const today = new Date().toISOString().slice(0, 10)
writeFileSync(
  join(dist, 'sitemap.xml'),
  `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${prebuiltPages.map((page) => `  <url><loc>${origin}${page.path}</loc><lastmod>${today}</lastmod></url>`).join('\n')}
</urlset>
`,
)

console.log(
  `Pre-built ${String(prebuiltPages.length)} pages, robots.txt and sitemap.xml for ${origin}`,
)

function readEnv(name) {
  const line = readFileSync(join(web, '.env'), 'utf8')
    .split('\n')
    .find((l) => l.startsWith(`${name}=`))
  if (!line) throw new Error(`${name} is not set`)
  return line.slice(name.length + 1).trim()
}
