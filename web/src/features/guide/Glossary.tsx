/**
 * How SyncBingo uses its words (the landing page and How to play): some mean other things elsewhere, so a
 * game, a round and the host each get a plain definition. Kept in step with docs/README's glossary.
 */
const glossary: { term: string; meaning: string }[] = [
  {
    term: 'Game',
    meaning:
      'Everything from the moment the host creates it, with its password, until the host ends it. A game can have many rounds, and players keep their names between them.',
  },
  {
    term: 'Round',
    meaning:
      'One go at winning, from Start Round until the cards are wiped clean of marks. Several people can win in one round, and the host can change what wins partway through.',
  },
  {
    term: 'Lobby',
    meaning:
      'The time between rounds. The host can deal new cards and change the round’s settings, and anyone who arrived during the round gets their card.',
  },
  {
    term: 'Host',
    meaning:
      'The person running the game: they draw the balls, check every Bingo and award the wins. The host doesn’t play; a host who wants to play too joins from a second phone.',
  },
  {
    term: 'Player',
    meaning:
      'Anyone who joined with the game code and the password. Each player has their own card on their own phone.',
  },
  {
    term: 'Game code and password',
    meaning:
      'The game code (16 letters and numbers, in four groups) finds the game, and the QR code carries it. The password is chosen by the host and typed by each player; it is never in the QR code.',
  },
  {
    term: 'Card',
    meaning:
      'Your grid of numbers, or of words with a custom set: 5 × 5, or 4 × 4 and 3 × 3 for quicker games. No two players in a game get the same card.',
  },
  {
    term: 'Free space',
    meaning:
      'The middle square. Usually it starts marked; the host can make players earn it (it counts once a ball is called that isn’t on their card) or have no free space at all.',
  },
  {
    term: 'Ball',
    meaning:
      'What the host draws, such as N 37: 75 balls on a 5 × 5 card, 60 on 4 × 4 and 45 on 3 × 3. With a custom set, a ball can be a sum, a word or a clue.',
  },
  {
    term: 'Call board',
    meaning:
      'Every ball, with the ones called so far lit up, so anyone who missed a call can check it.',
  },
  {
    term: 'Pattern',
    meaning:
      'What it takes to win this round: Normal Bingo (any full row, column or diagonal), Four Corners, a letter, Blackout (every square), or a shape the host drew. Several can be in play at once.',
  },
  {
    term: 'Bingo',
    meaning:
      'Pressing BINGO! tells the host you think you’ve won. The host checks your card against the balls actually called, then awards the win.',
  },
  {
    term: 'Waiting room',
    meaning:
      'Join while a round is being played and you wait here; you get your card as soon as the round ends.',
  },
  {
    term: 'Display',
    meaning:
      'The big-screen view for a TV or projector: the call board, the last ball, the join QR code and the winners, with no controls.',
  },
  {
    term: 'Custom set',
    meaning:
      'The host’s own balls and squares instead of numbers: “2 + 5” is called and players mark “7”, or a definition is called and they mark the word.',
  },
]

/** The glossary as a definition list. */
export function Glossary() {
  return (
    <dl className="flex flex-col gap-3">
      {glossary.map(({ term, meaning }) => (
        <div key={term}>
          <dt className="font-bold">{term}</dt>
          <dd className="text-ink-muted">{meaning}</dd>
        </div>
      ))}
    </dl>
  )
}
