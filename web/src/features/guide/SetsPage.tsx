import { Link } from 'react-router'
import { Article } from '../../app/Article'
import { librarySets } from './setLibrary'

/**
 * Ready-made sets (docs/06 §1): custom sets to download and play, with what each is for and a few of its
 * pairs. A host opens one in the set editor (Open a set file), or adds it to their own (Add from a file).
 */
export function SetsPage() {
  return (
    <Article title="Ready-made sets">
      <p>
        A custom set replaces the numbers with your own balls and squares: the host calls one thing,
        and players mark its match. These sets are ready to play. Download one, then in your game’s
        lobby choose <strong>Use a custom set</strong> and <strong>Open a set file</strong>. You can
        change any entry before you use it, or add it to a set of your own with{' '}
        <strong>Add from a file</strong>. See <Link to="/how-to-play">How to play</Link> for more on
        custom sets.
      </p>

      {librarySets.map((set) => (
        <section
          key={set.file}
          aria-labelledby={`set-${set.file}`}
          className="flex flex-col gap-2 rounded-3xl bg-surface p-5 shadow-card"
        >
          <h2 id={`set-${set.file}`} className="mt-0!">
            {set.name}
          </h2>
          <p className="text-sm text-ink-muted">
            {set.entries} entries ·{' '}
            {set.grouped
              ? 'in groups, one per column of a 5 × 5 card'
              : 'dealt anywhere on the card'}
          </p>
          <p>{set.about}</p>
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Examples from {set.name}</caption>
            <thead>
              <tr className="text-ink-muted">
                <th scope="col" className="pb-1 font-semibold">
                  The host calls
                </th>
                <th scope="col" className="pb-1 font-semibold">
                  Players mark
                </th>
              </tr>
            </thead>
            <tbody>
              {set.examples.map(([ball, square]) => (
                <tr key={square} className="border-t border-line-soft">
                  <td className="py-1 pr-3">{ball}</td>
                  <td className="py-1 font-semibold">{square}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            <a href={`/sets/${set.file}`} download>
              Download {set.name}
            </a>
          </p>
        </section>
      ))}

      <h2>Make your own</h2>
      <p>
        Any list of pairs works, as long as each ball and each square is different from the others,
        so a call matches only one square. Avoid sets whose answers repeat, such as simple sums (2 +
        2 and 1 + 3 both make 4). A 5 × 5 card needs at least 24 entries, a 3 × 3 card 8; more
        entries make more different cards. A set can be saved as a file and used again in any game.
      </p>
    </Article>
  )
}
