/**
 * The ready-made sets (`web/public/sets/`), for the Sets page: what each is for, how to play it, and a few of
 * its pairs. Tests check every file here is a valid set with this many entries.
 */
export interface LibrarySet {
  /** The file under /sets/. */
  file: string
  name: string
  /** Who it suits and how it plays. */
  about: string
  entries: number
  /** Dealt by group, one per card column (a 5 × 5 card), rather than anywhere. */
  grouped: boolean
  /** A few pairs as examples: what is called, and what players mark. */
  examples: [string, string][]
}

export const librarySets: LibrarySet[] = [
  {
    file: 'animal-sounds.json',
    name: 'Animal sounds',
    about:
      'For young children: the host calls a sound and players find the animal that makes it. The free space reads “Zoo”. Works on any card size.',
    entries: 28,
    grouped: false,
    examples: [
      ['Moo', 'Cow'],
      ['Quack', 'Duck'],
      ['Hee-haw', 'Donkey'],
    ],
  },
  {
    file: 'opposites.json',
    name: 'Opposites',
    about:
      'Early readers hear a word and mark its opposite. Short words, so it suits a 5 × 5 card on a small screen.',
    entries: 28,
    grouped: false,
    examples: [
      ['Hot', 'Cold'],
      ['Push', 'Pull'],
      ['Awake', 'Asleep'],
    ],
  },
  {
    file: 'grammar-words.json',
    name: 'Grammar words',
    about:
      'For older students: the host reads a definition and players mark the grammar term it describes. A friendly way to review parts of speech and punctuation.',
    entries: 28,
    grouped: false,
    examples: [
      ['A person, place, thing or idea.', 'Noun'],
      ['Letters added to the start of a word, like un- or re-.', 'Prefix'],
      ['A word that means the same as another, like big and large.', 'Synonym'],
    ],
  },
  {
    file: 'short-vowels.json',
    name: 'Short vowel sounds',
    about:
      'For early readers working on phonemes: one column for each short vowel sound (a as in cat, e as in bed, i as in pig, o as in hot, u as in bug), 15 simple words each. The host calls the word’s sounds, written with the short-vowel mark and in IPA (kăt /kæt/), and players find the word (cat) in its vowel’s column.',
    entries: 75,
    grouped: true,
    examples: [
      ['kăt /kæt/', 'cat'],
      ['bŏks /bɑks/', 'box'],
      ['bŭg /bʌɡ/', 'bug'],
    ],
  },
  {
    file: 'science-vocabulary.json',
    name: 'Science vocabulary',
    about:
      'Science terms for grade 6 and younger: the ball reads a clue, players mark the word. Five groups, one per column of a 5 × 5 card, so it plays like B-I-N-G-O: biology, inventions and tools, numbers and shapes, geology and Earth, objects and forces.',
    entries: 75,
    grouped: true,
    examples: [
      ['An organism that makes its own food using sunlight.', 'Producer'],
      ['The natural home where an animal or plant lives.', 'Habitat'],
      ['The force that slows things down when they rub together.', 'Friction'],
    ],
  },
]
