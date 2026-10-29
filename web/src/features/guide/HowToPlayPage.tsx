import { Link } from 'react-router'
import { Article } from '../../app/Article'
import { PatternGrid } from '../game/PatternGrid'
import { Glossary } from './Glossary'
import { examplePatterns } from './patternShapes'

/**
 * How to play (docs/06 §1): a guide for players and for hosts, written from how the game really works, so
 * it stays true when read beside the game. Also the site's main page of content for ad review (ADR 0012).
 */
export function HowToPlayPage() {
  return (
    <Article title="How to play SyncBingo">
      <p>
        SyncBingo is Bingo for any group, played on the devices everyone already has, in the same
        room or anywhere else. One person hosts and draws the balls; everyone else plays on their
        own device. This guide covers both sides. For quick answers, see the{' '}
        <Link to="/faq">FAQ</Link>.
      </p>

      <h2>For players</h2>

      <h3>Joining a game</h3>
      <ul>
        <li>
          Scan the QR code the host shows with your device’s camera, or open {window.location.host}{' '}
          and choose <strong>Join a game</strong>, then type the game code.
        </li>
        <li>
          Type the game password the host gives you, and a name of up to 20 characters. Everyone
          sees your name when you win, so a first name or a nickname is best. Two players can’t use
          the same name in one game.
        </li>
        <li>
          Press <strong>Get my card</strong>. If a round is already being played, you wait in the{' '}
          <strong>waiting room</strong> and get your card as soon as the round ends. Meanwhile you
          can choose how your marks look and follow the call board.
        </li>
        <li>
          Playing from somewhere else? Ask the host for the game code and password (for example in
          the call’s chat), then join as above by typing the code.
        </li>
      </ul>

      <h3>Your card</h3>
      <p>
        Your card is yours alone: no one else in the game has the same one. On a standard 5 × 5 card
        the columns are B, I, N, G and O, and each column holds numbers from its own range (B is 1
        to 15, I is 16 to 30, and so on). The middle square is the <strong>free space</strong>. The
        host may use a smaller card (4 × 4 or 3 × 3) for a quicker game, or a custom set of words or
        clues instead of numbers.
      </p>

      <h3>Marking</h3>
      <ul>
        <li>
          When a ball is called, tap the matching square to mark it. Tap again to remove a mark, if
          the host allows it.
        </li>
        <li>
          <strong>Undo</strong> always takes your card back to how it was when the last ball was
          called, so a slip is easy to fix.
        </li>
        <li>
          Depending on the game, you may be able to mark any square (a wrong mark simply doesn’t
          count) or only squares that have been called. Tap the pattern name at the top of your
          screen to see the rules in force.
        </li>
        <li>
          If the host offers <strong>auto-fill</strong>, you can switch it on and a ball you missed
          is marked for you when the next one is called.
        </li>
        <li>
          <strong>My marks</strong> changes how your marks look: a circle, star, heart, square or
          the whole square shaded, in the color, size and strength you like. You can also pick your
          own color theme. These choices stay on your device.
        </li>
      </ul>

      <h3>Calling Bingo</h3>
      <p>
        When your card has all squares marked for a winning pattern, press <strong>BINGO!</strong>.
        Your call goes to the host straight away. The host checks it against the balls actually
        called (a wrong mark never wins) and awards the win, and everyone’s screen shows “Ava had a
        Bingo!”. If you miss a Bingo, the host may still spot it: SyncBingo can alert the host to
        every card that has a winning line.
      </p>

      <h3>Patterns</h3>
      <p>
        The host chooses the current win conditions, and can pick several at once. The standard
        ones:
      </p>
      <div className="flex flex-wrap gap-5">
        {examplePatterns.map((pattern) => (
          <figure key={pattern.name} className="flex flex-col items-center gap-1">
            <PatternGrid
              mask={pattern.mask}
              columns={5}
              rows={5}
              freeCell={12}
              earned={false}
              label={pattern.name}
            />
            <figcaption className="max-w-28 text-center text-sm text-ink-muted">
              {pattern.name}
            </figcaption>
          </figure>
        ))}
      </div>
      <p>
        Normal Bingo wins with any full row, column or diagonal; the letters B, I, N, G and O each
        have their own shape; Blackout needs every square. The host can also draw patterns of their
        own. The ★ is the free space, which counts toward any pattern through it.
      </p>

      <h3>If your device goes to sleep</h3>
      <p>
        Your name, card and marks are kept by the game, not your device. Wake the device and the
        game catches up; or open the join link again in the same browser and choose{' '}
        <strong>Back to my card</strong>. To leave for good, use <strong>More</strong> and{' '}
        <strong>Leave game</strong>.
      </p>

      <h2>For hosts</h2>

      <h3>Starting a game</h3>
      <ul>
        <li>
          Choose <strong>Host a game</strong> and pick a password that’s easy to say out loud, such
          as the name of the event. Players type it to join; it keeps out anyone who mistypes a
          code.
        </li>
        <li>
          The lobby shows a QR code and the game code. Connect a separate display (TV or projector)
          from <strong>Display</strong>, read the code out, or on a video call share your screen or
          paste the code in the chat. Add a <strong>password note</strong> to show the password (or
          a hint) under the QR code on the TV.
        </li>
        <li>
          Players appear in the list as they join, up to 100. You can see any player’s card, and
          remove a player if you need to.
        </li>
      </ul>

      <h3>Choosing the rules</h3>
      <p>
        <strong>Options</strong> holds every rule, changed together with one <strong>Apply</strong>.
        Choose the patterns, the card size, the free space (always marked, earned or none), how
        forgiving marking is, whether players see the called ball, the call board and highlighted
        squares, whether auto-fill is offered, how many times a card can win in a round, and a color
        theme for everyone’s screens. Some settings can only be set between rounds, such as the card
        size. For young children, keep marking to called squares and turn on auto-fill; for adults
        who want to concentrate, turn the help off.
      </p>

      <h3>Running a round</h3>
      <ul>
        <li>
          Press <strong>Start Round</strong>. A 10-second countdown shows the round’s pattern on
          every screen, then the round begins.
        </li>
        <li>
          In <strong>manual</strong> mode, press <strong>Grab ball</strong> for each ball and call
          it out. In <strong>auto</strong> mode, a ball is drawn on a timer (10 seconds by default);
          press <strong>Pause</strong> to go back to manual, for a prize or a break.
        </li>
        <li>
          You can change the pattern mid-round, say from Normal Bingo to Blackout, without clearing
          the cards. Everyone is told what changed, and the next ball waits a moment so they can
          read it.
        </li>
      </ul>

      <h3>Checking a Bingo</h3>
      <p>
        Every press of BINGO! reaches you, valid or not, with what the card really has on the called
        balls. <strong>Inspect</strong> shows the card; <strong>Award</strong> credits the win and
        tells everyone; <strong>Dismiss</strong> clears a false call. Someone shouted Bingo instead
        of pressing the button? Award them from their row in the players list. With win detection
        on, SyncBingo also alerts you to any card with a winning line, noticed or not, and can pause
        auto mode when one appears. You always decide who wins.
      </p>

      <h3>Between rounds</h3>
      <p>
        <strong>End round</strong> stops calling and leaves the cards as they are, so everyone can
        see what happened. <strong>Clear cards</strong> wipes the marks for a fresh round, and{' '}
        <strong>Deal New Cards</strong> gives everyone a new card. Anyone in the waiting room gets
        their card as soon as you’re back in the lobby. The <strong>Leaderboard</strong> counts wins
        across the whole game, and you can show it on every screen.
      </p>

      <h3>The big screen</h3>
      <p>
        <strong>Display</strong> opens a read-only view for a TV or projector: the call board, the
        last ball, the pattern, the join QR code between rounds, and every win. Open it in a new tab
        on a device connected to the TV, such as a laptop, or connect a smart TV directly: open{' '}
        {window.location.host}
        /tv in the TV’s browser and type the code it shows under <strong>Display</strong> and{' '}
        <strong>Connect a TV</strong>.
      </p>

      <h3>Custom sets</h3>
      <p>
        Instead of numbers, play with your own balls and squares: for example, “Moo” is called and
        players mark “Cow”, or “Person, place, thing or idea” is called and they mark “Noun”. In the
        lobby, choose <strong>Use a custom set</strong> to build one (8 to 500 entries), optionally
        in groups, one for each card column. Download the set as a file to use again in another
        game. It’s a favorite for classrooms: vocabulary, parts of speech, science terms.
      </p>

      <h3>Ending the game</h3>
      <p>
        When you’re done, choose <strong>End game</strong>. Every screen shows that the game has
        ended, and everything about it is deleted a few minutes later.
      </p>

      <h2>Words SyncBingo uses</h2>
      <Glossary />
    </Article>
  )
}
