import type { ReactNode } from 'react'
import { Link } from 'react-router'

/**
 * Terms of Use and Privacy (roadmap Phase 3): plain-language DRAFTS that describe what the app actually
 * does, to be reviewed before relying on them. Bracketed placeholders are facts only the operator can fill.
 */
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
    <article className="mx-auto flex w-full max-w-2xl flex-col gap-4 pb-6 leading-relaxed [&_h2]:mt-3 [&_h2]:font-display [&_h2]:text-xl [&_h2]:font-bold [&_li]:ml-5 [&_li]:list-disc">
      <p
        role="note"
        className="rounded-2xl border-2 border-notice-line bg-notice p-3 font-semibold text-notice-ink"
      >
        Draft, pending legal review. This page describes how SyncBingo works today; it is not yet
        final.
      </p>
      <h1 className="font-display text-3xl font-extrabold tracking-tight">{title}</h1>
      <p className="text-sm text-ink-muted">Last updated {updated}</p>
      {children}
      <p>
        <Link to="/" className="font-semibold text-accent-text underline">
          Back to SyncBingo
        </Link>
      </p>
    </article>
  )
}

export function PrivacyPage() {
  return (
    <LegalPage title="Privacy" updated="16 October 2026">
      <p>
        SyncBingo is a party Bingo game. It has no accounts and no sign-up, and it keeps as little
        as it can, for as short a time as it can. It is run by Nathan Holst; questions go to
        [CONTACT EMAIL].
      </p>

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
          Random tokens that let each phone, the host and any TV get back into the game after a
          reload; the server keeps only their hashes.
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
        Your browser keeps your game token (so a sleeping phone can rejoin), your light or dark
        choice and how you like your marks to look. A TV keeps its display token only until its tab
        is closed. SyncBingo uses no cookies, no analytics and no tracking.
      </p>

      <h2>Who else sees it</h2>
      <p>
        No one. Data isn&apos;t sold or shared. The site runs on Amazon Web Services in the United
        States, and its domain&apos;s DNS is at Cloudflare; the fonts are served by SyncBingo
        itself. The ad spaces are empty placeholders today. If real ads are added, this page will
        say so before they appear.
      </p>

      <h2>Children</h2>
      <p>
        Families and classrooms play SyncBingo. It collects no personal information from anyone
        beyond the name typed to play, which is gone within hours. A grown-up hosting for children
        can suggest first names or nicknames.
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
        SyncBingo is a free party Bingo game run by Nathan Holst. By hosting or joining a game you
        agree to these terms. Questions go to [CONTACT EMAIL].
      </p>

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
        law allows, Nathan Holst isn&apos;t liable for any loss from using it.
      </p>

      <h2>Changes</h2>
      <p>These terms may be updated; the date above shows the latest version.</p>
    </LegalPage>
  )
}
