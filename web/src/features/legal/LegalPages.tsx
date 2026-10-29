import type { ReactNode } from 'react'
import { Article } from '../../app/Article'
import { adConfig } from '../../app/ads'
import { site } from '../../site'

/**
 * Terms of Use and Privacy (roadmap Phase 3): plain-language DRAFTS that describe what the app actually
 * does, to be reviewed before relying on them. The operator's name and addresses come from the build's
 * settings (`web/.env`, see `site.ts`).
 */
function Email({ address }: { address: string }) {
  return (
    <a href={`mailto:${address}`} className="font-semibold text-accent-text underline">
      {address}
    </a>
  )
}

function Contacts() {
  return (
    <p>
      Questions about this page go to <Email address={site.legalEmail} />; for help with a game,
      write to <Email address={site.supportEmail} />.
    </p>
  )
}

function LegalPage({
  title,
  updated,
  children,
}: {
  title: string
  updated: string
  children: ReactNode
}) {
  return (
    <Article
      title={title}
      before={
        <p
          role="note"
          className="rounded-2xl border-2 border-notice-line bg-notice p-3 font-semibold text-notice-ink"
        >
          Draft, pending legal review. This page describes how SyncBingo works today; it is not yet
          final.
        </p>
      }
    >
      <p className="text-sm text-ink-muted">Last updated {updated}</p>
      {children}
    </Article>
  )
}

export function PrivacyPage() {
  // The page describes the build it is part of: real ads only where the build shows them (ADR 0012).
  const ads = adConfig().provider === 'adsense'
  return (
    <LegalPage title="Privacy" updated={ads ? '27 October 2026' : '16 October 2026'}>
      <p>
        SyncBingo is a party Bingo game. It has no accounts and no sign-up, and it keeps as little
        as it can, for as short a time as it can. It is run by {site.operator}.
      </p>
      <Contacts />

      <h2>What a game stores</h2>
      <ul>
        <li>
          The game password the host chooses, stored only as a one-way hash, never as the password.
        </li>
        <li>
          The name each player types, their card, their marks, and the game&apos;s calls and wins.
        </li>
        <li>An optional note the host types to show on the TV with the join QR.</li>
        <li>
          Random tokens that let each player’s device, the host and any TV get back into the game
          after a reload; the server keeps only their hashes.
        </li>
      </ul>
      <p>
        Use a first name or a nickname to play. SyncBingo doesn&apos;t ask for email addresses,
        phone numbers, ages or any other personal details, and it doesn&apos;t need them.
      </p>

      <h2>How long it is kept</h2>
      <ul>
        <li>5 minutes after the host ends the game, then everything about it is deleted.</li>
        <li>4 hours after the last activity, if a game is simply left.</li>
        <li>12 hours at most for any game, however busy.</li>
      </ul>
      <p>There are no backups or archives of games.</p>

      <h2>Your IP address</h2>
      <p>
        The server sees each device&apos;s IP address, as every website does. SyncBingo uses it only
        for a few minutes, in memory, to limit repeated wrong passwords and game creation. It
        isn&apos;t stored, and the site keeps no access logs.
      </p>

      <h2>On your device</h2>
      <p>
        Your browser keeps your game token (so a device that went to sleep can rejoin), your light
        or dark choice and how you like your marks to look. A TV keeps its display token only until
        its tab is closed. SyncBingo uses no cookies, no analytics and no tracking
        {ads ? '; the ads come from Google, which works differently (see Ads below).' : '.'}
      </p>

      <h2>Who else sees it</h2>
      {ads ? (
        <p>
          No one gets your game. Data isn&apos;t sold, and SyncBingo never sends your name, card or
          anything about your game to anyone. The site runs on Amazon Web Services in the United
          States, and its domain&apos;s DNS is at Cloudflare; the fonts are served by SyncBingo
          itself. The ads come from Google, as described next.
        </p>
      ) : (
        <p>
          No one. Data isn&apos;t sold or shared. The site runs on Amazon Web Services in the United
          States, and its domain&apos;s DNS is at Cloudflare; the fonts are served by SyncBingo
          itself. The ad spaces are empty placeholders today. If real ads are added, this page will
          say so before they appear.
        </p>
      )}

      {ads && (
        <>
          <h2>Ads</h2>
          <p>
            The banner spaces show ads from Google AdSense, which pay for running SyncBingo. The TV
            display never shows ads.
          </p>
          <ul>
            <li>
              SyncBingo asks Google to treat every ad request as directed at children, so there are
              no personalized ads and no remarketing: an ad is chosen for the page, not for you.
            </li>
            <li>
              To show an ad, your browser fetches it from Google. Google receives your IP address
              and browser details, and may use cookies or similar storage on your device, for
              example to limit how often an ad repeats and to stop fraud.
            </li>
            <li>
              In the EU, the UK and Switzerland you&apos;re asked first. You can change your choice
              at any time with <strong>Privacy and cookie settings</strong> at the bottom of every
              page.
            </li>
          </ul>
          <p>
            Google explains what it does with this in{' '}
            <a
              href="https://policies.google.com/technologies/partner-sites"
              className="font-semibold text-accent-text underline"
            >
              How Google uses information from sites that use its services
            </a>
            .
          </p>
        </>
      )}

      <h2>Children</h2>
      <p>
        Families and classrooms play SyncBingo. {ads ? 'SyncBingo itself' : 'It'} collects no
        personal information from anyone beyond the name typed to play, which is gone within hours.
        A grown-up hosting for children can suggest first names or nicknames.
        {ads && ' Every ad is requested as directed at children, so none is personalized.'}
      </p>

      <h2>Changes</h2>
      <p>If this page changes, the date above changes with it.</p>
    </LegalPage>
  )
}

export function TermsPage() {
  return (
    <LegalPage title="Terms of Use" updated="16 October 2026">
      <p>
        SyncBingo is a free party Bingo game run by {site.operator}. By hosting or joining a game
        you agree to these terms.
      </p>
      <Contacts />

      <h2>Playing fair and kind</h2>
      <ul>
        <li>Choose names that are fine for everyone in the room; offensive names are refused.</li>
        <li>
          Don&apos;t try to break, overload or misuse the service, or other people&apos;s games.
        </li>
        <li>Don&apos;t use SyncBingo for anything unlawful.</li>
      </ul>

      <h2>No money</h2>
      <p>
        SyncBingo never takes payments or handles money. Games played for money or prizes may be
        regulated where you are; if you run one, it is up to you to follow the rules that apply.
      </p>

      <h2>Hosts</h2>
      <p>
        The host runs the game and decides who wins. A host is responsible for what they type into
        it, such as the password note shown on screens, and for any content they add.
      </p>

      <h2>The service</h2>
      <p>
        SyncBingo is provided as it is, without guarantees. It may be unavailable at times, games
        can end when the server restarts or expires them, and features may change. To the extent the
        law allows, {site.operator} isn&apos;t liable for any loss from using it.
      </p>

      <h2>Changes</h2>
      <p>These terms may be updated; the date above shows the latest version.</p>
    </LegalPage>
  )
}
