import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Article } from '../../app/Article'
import { site } from '../../site'

/** Short answers to what people ask before and during a game (docs/06 §1), true to how SyncBingo works. */
const questions: { q: string; a: ReactNode }[] = [
  {
    q: 'Do players need an app or an account?',
    a: 'No. SyncBingo runs in a web browser, on a phone, tablet or computer. Players scan a QR code (or type a game code), type the password the host gives them and a name, and they’re playing. Nobody signs up.',
  },
  {
    q: 'Is it free?',
    a: 'Yes, for hosts and players. Ads on some pages help pay for running the site.',
  },
  {
    q: 'How many people can play?',
    a: 'Up to 100 players in one game, plus as many TVs or projectors as you need (up to 10).',
  },
  {
    q: 'What does the host need?',
    a: 'Any device with a browser. A larger screen, such as a laptop’s, works best, since it holds the call board, the players and the Bingo calls side by side. A TV or projector, or a shared screen on a video call, helps everyone follow along, but it isn’t required.',
  },
  {
    q: 'Do players have to be in the same room?',
    a: 'No. Players only need the game code and the password, which the host can share in person, on a call or in a message. The balls appear on every player’s device (unless the host turns that off), so a group can play from anywhere. On a video call, the host can also share their screen to show the call board.',
  },
  {
    q: 'Can the host play too?',
    a: 'Not from the host’s screen: the host runs the game and decides every win, so it stays fair. A host who wants to play joins from a second device, like everyone else.',
  },
  {
    q: 'Can someone join after the game has started?',
    a: 'Yes. Between rounds they get a card straight away. During a round they wait in the waiting room and get their card as soon as the round ends.',
  },
  {
    q: 'What happens if my device goes to sleep?',
    a: 'Nothing is lost. Your name, card and marks are kept by the game; when you come back, your screen catches up with every ball called while you were away.',
  },
  {
    q: 'Who decides whether a Bingo counts?',
    a: (
      <>
        The host. Pressing BINGO! sends your call to the host, who sees exactly which pattern your
        card completes on the balls really called, and awards the win. A square you marked by
        mistake never counts. See <Link to="/how-to-play">How to play</Link> for the details.
      </>
    ),
  },
  {
    q: 'Can more than one person win a round?',
    a: 'Yes. Several players can win the same round, and the host can change the pattern partway through, for example from Normal Bingo to Blackout. The host also chooses whether one card can win once per round, once per pattern, or for every line it completes.',
  },
  {
    q: 'Is it good for young children?',
    a: 'It was made with them in mind. The host can let players mark only called squares, offer auto-fill for missed balls, highlight called squares and celebrate wins. Undo is always there for a slip, and offensive names are refused.',
  },
  {
    q: 'Can we play with words instead of numbers?',
    a: 'Yes, with a custom set: the host enters pairs of what is called and what players mark, such as a definition and its word, or an animal’s sound and the animal. Teachers use it for vocabulary, parts of speech and science terms. A set can be saved as a file and used again.',
  },
  {
    q: 'Can we play a shorter game?',
    a: 'Choose a smaller card: 4 × 4 uses 60 balls and 3 × 3 uses 45, so a round ends sooner. Four Corners and the host’s own small patterns are quick too.',
  },
  {
    q: 'How long is a game kept?',
    a: 'Until the host ends it. A game nobody touches for 4 hours ends by itself, and no game lasts more than 12 hours. Once a game ends, everything about it is deleted within minutes.',
  },
  {
    q: 'What does SyncBingo keep about me?',
    a: (
      <>
        Only the name you type, your card and marks, and the game’s calls and wins, for as long as
        the game lasts. No email, no phone number, no account. The{' '}
        <Link to="/privacy">Privacy</Link> page has the details.
      </>
    ),
  },
  {
    q: 'Can we play for money?',
    a: (
      <>
        SyncBingo never takes payments or handles money. Games played for money or prizes may be
        regulated where you live; if you run one, following those rules is up to you (see the{' '}
        <Link to="/terms">Terms</Link>).
      </>
    ),
  },
  {
    q: 'Something isn’t working. Who can I ask?',
    a: (
      <>
        Write to <a href={`mailto:${site.supportEmail}`}>{site.supportEmail}</a> with what you saw,
        and on which device.
      </>
    ),
  },
]

export function FaqPage() {
  return (
    <Article title="Frequently asked questions">
      {questions.map(({ q, a }) => (
        <section key={q} aria-label={q} className="flex flex-col gap-1">
          <h2>{q}</h2>
          <p>{a}</p>
        </section>
      ))}
    </Article>
  )
}
