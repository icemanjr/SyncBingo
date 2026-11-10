/**
 * The pages the build renders to HTML ahead of time (docs/07, ADR 0012 amended): what search engines and
 * reviewers read without running the app. Each gets its own title, description and canonical link, and is
 * listed in the sitemap. Every other route is the app, served from the empty shell (`app.html`).
 */
export const prebuiltPages: { path: string; title: string; description: string }[] = [
  {
    path: '/',
    title: 'SyncBingo: Bingo that keeps everyone in sync',
    description:
      'Free Bingo for any group: one person hosts, everyone else joins with a game code and plays on their own device, in the same room or anywhere. No app, no sign-up. For parties, families and classrooms.',
  },
  {
    path: '/how-to-play',
    title: 'How to play SyncBingo · SyncBingo',
    description:
      'How to join a game, mark your card and call Bingo, and how to host: rules, patterns, rounds, checking a Bingo, the TV display and custom sets.',
  },
  {
    path: '/faq',
    title: 'Frequently asked questions · SyncBingo',
    description:
      'Short answers about SyncBingo: no app or account, up to 100 players, playing apart, who decides a Bingo, children, custom sets and privacy.',
  },
  {
    path: '/sets',
    title: 'Ready-made sets · SyncBingo',
    description:
      'Free Bingo sets to download and play: animal sounds, opposites, grammar words and science vocabulary, for classrooms and families.',
  },
  {
    path: '/terms',
    title: 'Terms of Use · SyncBingo',
    description: 'The terms for hosting or joining a SyncBingo game.',
  },
  {
    path: '/privacy',
    title: 'Privacy · SyncBingo',
    description:
      'What SyncBingo keeps (as little as it can, for as short a time as it can) and who else sees it.',
  },
]
