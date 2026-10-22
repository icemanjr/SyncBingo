import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { setDrafts } from '../../storage/setDrafts'
import { seats } from '../../storage/sessions'
import { normalizeCode } from '../game/codes'
import { applySetFile, backToNumbers, fetchSetFile, saveFile } from '../game/downloadSet'
import { useAction } from '../game/useAction'
import { useGame } from '../game/useGame'
import { Button, ErrorText } from '../game/ui'
import {
  addEntry,
  addGroup,
  deleteEntry,
  deleteGroup,
  describe,
  describeMerge,
  editEntry,
  emptyDraft,
  mergeDraft,
  moveEntry,
  moveGroup,
  renameGroup,
  setLabel,
  setProblems,
  squareOf,
  type Draft,
  type DraftEntry,
  type DraftGroup,
} from './draft'
import { readSetFile, writeSetFile } from './setFile'
import { limits, tidy } from './text'

const input =
  'min-h-12 w-full rounded-xl border-2 border-line bg-surface px-3 focus-visible:outline-4 focus-visible:outline-accent'

/** A file the host picked, as text. */
const readFile = (file: File): Promise<string> => file.text()

/**
 * The set editor (FR-44, ADR 0009): the host builds a custom set as a **draft** on this device, then sends
 * it whole with **Use this set**, so players get one redeal. Entries show in their groups, in column order,
 * with any ungrouped ones first; groups can be renamed (their entries follow), relabelled, reordered and
 * deleted; entries edited, moved and deleted. Upload replaces the draft; Add from file merges another set,
 * reporting what it skipped. The draft is kept in this browser per game.
 */
export function SetEditor() {
  const code = normalizeCode(useParams().code ?? '')
  const seat = seats.load(code)
  const token = seat?.role === 'host' ? seat.token : undefined
  if (!token) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="font-display text-3xl font-extrabold">Custom set</h1>
        <p>Only the host can edit this game’s set, from the device that created the game.</p>
        <Link to="/" className="font-semibold text-accent-text underline">
          Back to SyncBingo
        </Link>
      </div>
    )
  }

  return <Editor code={code} token={token} />
}

function Editor({ code, token }: { code: string; token: string }) {
  const game = useGame(code, token)
  const navigate = useNavigate()
  const stored = useMemo(() => setDrafts.load(code), [code])
  const [draft, setDraft] = useState<Draft>(stored?.draft ?? emptyDraft())
  const [groupItems, setGroupItems] = useState(stored?.groupItems ?? true)
  const started = useRef(stored !== undefined)
  const [message, setMessage] = useState<string>()
  const [search, setSearch] = useState('')
  const [acknowledged, setAcknowledged] = useState(false)
  const action = useAction()
  const content = game.snapshot?.game.content
  const lobby = game.snapshot?.game.state === 'BetweenRounds'

  // Start from the set in use (once), unless this browser already has a draft for the game.
  useEffect(() => {
    if (started.current || !game.snapshot) return
    started.current = true
    if (!content) return
    void action.run(async () => {
      const read = readSetFile(await fetchSetFile(code, token))
      if ('draft' in read) {
        setDraft(read.draft)
        setGroupItems(content.groupItems)
      }
    })
  }, [game.snapshot, content, code, token, action])

  useEffect(() => {
    setDrafts.save(code, { draft, groupItems })
  }, [code, draft, groupItems])

  const problems = setProblems(draft, groupItems)
  const ungrouped = draft.entries.filter((entry) => entry.groupId === null)
  const filter = (entries: DraftEntry[]) => {
    const term = tidy(search).toLowerCase()
    return term
      ? entries.filter(
          (e) => e.ball.toLowerCase().includes(term) || squareOf(e).toLowerCase().includes(term),
        )
      : entries
  }

  const replaceFrom = async (file: File, merge: boolean) => {
    const read = readSetFile(await readFile(file))
    if ('error' in read) {
      setMessage(read.error)
      return
    }

    if (merge) {
      const merged = mergeDraft(draft, read.draft)
      setDraft(merged.draft)
      setMessage(describeMerge(merged.report))
    } else {
      setDraft(read.draft)
      setGroupItems(read.draft.groups.length > 0)
      setMessage(
        `Opened “${read.draft.name || 'Custom set'}”: ${String(read.draft.entries.length)} entries.`,
      )
    }
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to={`/host/${code}`} className="text-sm font-semibold text-accent-text underline">
            Back to the game
          </Link>
          <h1 className="font-display text-3xl font-extrabold">Custom set</h1>
        </div>
        <p className="text-sm text-ink-muted">
          {draft.entries.length} of {limits.maxEntries} entries
        </p>
      </div>

      <p className="rounded-2xl border-2 border-notice-line bg-notice p-3 text-sm text-notice-ink">
        You are responsible for this set’s content: it is used only in your games, and it should
        suit everyone playing (see the{' '}
        <Link to="/terms" className="font-semibold underline">
          Terms
        </Link>
        ). Changes stay on this device until you press <b>Use this set</b>.
      </p>

      <section
        aria-label="The set"
        className="flex flex-col gap-3 rounded-3xl bg-surface p-4 shadow-card"
      >
        <label className="flex flex-col gap-1 font-semibold">
          Set name
          <input
            className={input}
            value={draft.name}
            maxLength={limits.name}
            placeholder="Week 5 vocabulary"
            onChange={(e) => {
              setDraft({ ...draft, name: e.target.value })
            }}
          />
        </label>
        <div className="flex flex-wrap gap-2">
          <FilePick label="Open a set file" onPick={(file) => void replaceFrom(file, false)} />
          <FilePick
            label="Add from a file"
            look="secondary"
            onPick={(file) => void replaceFrom(file, true)}
          />
          <Button
            look="secondary"
            disabled={draft.entries.length === 0}
            onClick={() => {
              saveFile(`${tidy(draft.name) || 'Custom set'}.json`, writeSetFile(draft))
            }}
          >
            Download this draft
          </Button>
        </div>
        {message && (
          <p role="status" className="text-sm font-medium">
            {message}
          </p>
        )}
      </section>

      <AddEntryForm
        draft={draft}
        onAdd={(ball, square, groupId) => {
          const result = addEntry(draft, ball, square, groupId)
          if (result.problem) return describe(result.problem)
          setDraft(result.draft)
          return undefined
        }}
      />

      <AddGroupForm
        draft={draft}
        onAdd={(name) => {
          const result = addGroup(draft, name)
          if (result.problem) return result.problem
          setDraft(result.draft)
          return undefined
        }}
      />

      {draft.entries.length > 20 && (
        <label className="flex flex-col gap-1 font-semibold">
          Find an entry
          <input
            type="search"
            className={input}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
            }}
          />
        </label>
      )}

      {(ungrouped.length > 0 || draft.groups.length === 0) && (
        <section
          aria-label="Not in a group"
          className="flex flex-col gap-2 rounded-3xl bg-surface p-4 shadow-card"
        >
          <h2 className="font-display text-xl font-bold">
            {draft.groups.length === 0 ? 'Entries' : 'Not in a group'} ({ungrouped.length})
          </h2>
          {groupItems && draft.groups.length > 0 && ungrouped.length > 0 && (
            <p
              role="alert"
              className="rounded-xl bg-notice p-2 text-sm font-semibold text-notice-ink"
            >
              Group items is on, so the set can’t be used until these are in a group.
            </p>
          )}
          <EntryList entries={filter(ungrouped)} draft={draft} setDraft={setDraft} />
        </section>
      )}

      {draft.groups.map((group, index) => (
        <GroupSection
          key={group.id}
          group={group}
          first={index === 0}
          last={index === draft.groups.length - 1}
          draft={draft}
          setDraft={setDraft}
          entries={filter(draft.entries.filter((entry) => entry.groupId === group.id))}
        />
      ))}

      <section
        aria-label="Use this set"
        className="flex flex-col gap-3 rounded-3xl bg-surface p-4 shadow-card"
      >
        <label className="flex min-h-12 items-start gap-3">
          <input
            type="checkbox"
            checked={groupItems}
            className="mt-1 h-5 w-5 accent-accent"
            onChange={(e) => {
              setGroupItems(e.target.checked)
            }}
          />
          <span>
            <span className="font-semibold">Group items</span>
            <span className="block text-sm text-ink-muted">
              Each card column draws from one group, in this order, like B-I-N-G-O. Off: entries go
              anywhere on the card.
            </span>
          </span>
        </label>
        {problems.length > 0 && (
          <ul className="list-disc pl-5 text-sm font-medium text-danger">
            {problems.map((problem) => (
              <li key={problem}>{problem}</li>
            ))}
          </ul>
        )}
        {!lobby && game.snapshot && (
          <p className="text-sm text-ink-muted">The set can change between rounds, in the lobby.</p>
        )}
        <label className="flex min-h-12 items-start gap-3">
          <input
            type="checkbox"
            checked={acknowledged}
            className="mt-1 h-5 w-5 accent-accent"
            onChange={(e) => {
              setAcknowledged(e.target.checked)
            }}
          />
          <span className="text-sm">
            I’m responsible for this set’s content and it suits the people playing.
          </span>
        </label>
        <div className="flex flex-wrap gap-2">
          <Button
            disabled={action.busy || problems.length > 0 || !lobby || !acknowledged}
            onClick={() =>
              void action.run(async () => {
                await applySetFile(code, token, writeSetFile(draft), groupItems)
                setDrafts.forget(code)
                await navigate(`/host/${code}`)
              })
            }
          >
            Use this set
          </Button>
          {content && (
            <Button
              look="outline"
              disabled={action.busy || !lobby}
              onClick={() =>
                void action.run(async () => {
                  await backToNumbers(code, token)
                  await navigate(`/host/${code}`)
                })
              }
            >
              Back to numbers
            </Button>
          )}
        </div>
        <p className="text-sm text-ink-muted">
          Using a set, or going back to numbers, gives every player a new card.
        </p>
        {action.error && <ErrorText>{action.error}</ErrorText>}
      </section>
    </div>
  )
}

/** A button that opens the file picker for a set file. */
function FilePick({
  label,
  look = 'primary',
  onPick,
}: {
  label: string
  look?: 'primary' | 'secondary'
  onPick: (file: File) => void
}) {
  const style =
    look === 'primary'
      ? 'bg-accent text-accent-ink hover:bg-accent-strong'
      : 'bg-accent-soft text-accent-text hover:bg-accent-soft/70'
  return (
    <label
      className={`inline-flex min-h-12 cursor-pointer items-center justify-center rounded-2xl px-5 font-bold focus-within:outline-4 focus-within:outline-accent ${style}`}
    >
      {label}
      <input
        type="file"
        accept="application/json,.json"
        className="sr-only"
        onChange={(e) => {
          const file = e.target.files?.[0]
          e.target.value = '' // the same file can be picked again
          if (file) onPick(file)
        }}
      />
    </label>
  )
}

/** Add an entry: what is called, what players mark (hidden when they match), and its group. */
function AddEntryForm({
  draft,
  onAdd,
}: {
  draft: Draft
  onAdd: (ball: string, square: string | null, groupId: string | null) => string | undefined
}) {
  const [ball, setBall] = useState('')
  const [square, setSquare] = useState('')
  const [matched, setMatched] = useState(true)
  const [groupId, setGroupId] = useState<string>('')
  const [problem, setProblem] = useState<string>()

  const submit = (e: { preventDefault: () => void }) => {
    e.preventDefault()
    const refused = onAdd(ball, matched ? null : square, groupId || null)
    setProblem(refused)
    if (!refused) {
      setBall('')
      setSquare('')
    }
  }

  return (
    <form
      onSubmit={submit}
      aria-label="Add an entry"
      className="flex flex-col gap-3 rounded-3xl bg-surface p-4 shadow-card"
    >
      <h2 className="font-display text-xl font-bold">Add an entry</h2>
      <label className="flex min-h-12 items-center gap-3">
        <input
          type="checkbox"
          checked={matched}
          className="h-5 w-5 accent-accent"
          onChange={(e) => {
            setMatched(e.target.checked)
          }}
        />
        Ball and square match
      </label>
      <label className="flex flex-col gap-1 font-semibold">
        {matched ? 'Word or phrase' : 'What is called'}
        <input
          className={input}
          value={ball}
          maxLength={matched ? limits.square * 3 : limits.ball * 3}
          placeholder={
            matched ? 'photosynthesis' : 'An organism that makes its own food using sunlight.'
          }
          onChange={(e) => {
            setBall(e.target.value)
          }}
        />
      </label>
      {!matched && (
        <label className="flex flex-col gap-1 font-semibold">
          What players mark
          <input
            className={input}
            value={square}
            maxLength={limits.square * 3}
            placeholder="Producer"
            onChange={(e) => {
              setSquare(e.target.value)
            }}
          />
        </label>
      )}
      {draft.groups.length > 0 && (
        <label className="flex flex-col gap-1 font-semibold">
          Group
          <select
            className={input}
            value={groupId}
            onChange={(e) => {
              setGroupId(e.target.value)
            }}
          >
            <option value="">Not in a group</option>
            {draft.groups.map((group) => (
              <option key={group.id} value={group.id}>
                {group.name}
              </option>
            ))}
          </select>
        </label>
      )}
      {problem && <ErrorText>{problem}</ErrorText>}
      <Button type="submit" look="secondary" disabled={!tidy(ball)}>
        Add entry
      </Button>
    </form>
  )
}

/** Add a group, at the end (the next card column). */
function AddGroupForm({
  draft,
  onAdd,
}: {
  draft: Draft
  onAdd: (name: string) => string | undefined
}) {
  const [name, setName] = useState('')
  const [problem, setProblem] = useState<string>()
  return (
    <form
      aria-label="Add a group"
      className="flex flex-wrap items-end gap-2"
      onSubmit={(e) => {
        e.preventDefault()
        const refused = onAdd(name)
        setProblem(refused)
        if (!refused) setName('')
      }}
    >
      <label className="flex min-w-48 flex-1 flex-col gap-1 font-semibold">
        New group
        <input
          className={input}
          value={name}
          maxLength={limits.groupName * 3}
          placeholder="Biology"
          onChange={(e) => {
            setName(e.target.value)
          }}
        />
      </label>
      <Button
        type="submit"
        look="secondary"
        disabled={!tidy(name) || draft.groups.length >= limits.groups}
      >
        Add group
      </Button>
      {problem && <ErrorText>{problem}</ErrorText>}
    </form>
  )
}

/** One group: its name (renamed once, for all its entries), short label, place in the column order, entries. */
function GroupSection({
  group,
  first,
  last,
  draft,
  setDraft,
  entries,
}: {
  group: DraftGroup
  first: boolean
  last: boolean
  draft: Draft
  setDraft: (draft: Draft) => void
  entries: DraftEntry[]
}) {
  const [renaming, setRenaming] = useState(false)
  const [name, setName] = useState(group.name)
  const [problem, setProblem] = useState<string>()
  const count = draft.entries.filter((entry) => entry.groupId === group.id).length

  return (
    <section
      aria-label={`Group ${group.name}`}
      className="flex flex-col gap-2 rounded-3xl bg-surface p-4 shadow-card"
    >
      <div className="flex flex-wrap items-center gap-2">
        {renaming ? (
          <form
            className="flex flex-1 flex-wrap gap-2"
            onSubmit={(e) => {
              e.preventDefault()
              const result = renameGroup(draft, group.id, name)
              setProblem(result.problem)
              if (!result.problem) {
                setDraft(result.draft)
                setRenaming(false)
              }
            }}
          >
            <label className="sr-only" htmlFor={`name-${group.id}`}>
              Group name
            </label>
            <input
              id={`name-${group.id}`}
              className={`${input} flex-1`}
              value={name}
              onChange={(e) => {
                setName(e.target.value)
              }}
            />
            <Button type="submit" look="secondary">
              Save
            </Button>
          </form>
        ) : (
          <h2 className="flex-1 font-display text-xl font-bold">
            {group.name} <span className="text-base font-medium text-ink-muted">({count})</span>
          </h2>
        )}
        <label className="flex items-center gap-2 text-sm font-semibold">
          Label
          <input
            className="min-h-12 w-20 rounded-xl border-2 border-line bg-surface px-2 text-center font-display font-bold"
            defaultValue={group.label}
            maxLength={limits.label * 3}
            aria-describedby={`label-help-${group.id}`}
            onBlur={(e) => {
              const result = setLabel(draft, group.id, e.target.value)
              setProblem(result.problem)
              if (!result.problem) setDraft(result.draft)
            }}
          />
        </label>
        <span id={`label-help-${group.id}`} className="sr-only">
          Up to {limits.label} characters, above this group’s card column.
        </span>
      </div>
      <div className="flex flex-wrap gap-2">
        {!renaming && (
          <Button
            look="quiet"
            onClick={() => {
              setName(group.name)
              setRenaming(true)
            }}
          >
            Rename
          </Button>
        )}
        <Button
          look="quiet"
          disabled={first}
          onClick={() => {
            setDraft(moveGroup(draft, group.id, -1))
          }}
        >
          Move up
        </Button>
        <Button
          look="quiet"
          disabled={last}
          onClick={() => {
            setDraft(moveGroup(draft, group.id, 1))
          }}
        >
          Move down
        </Button>
        <Button
          look="quiet"
          onClick={() => {
            setDraft(deleteGroup(draft, group.id))
          }}
        >
          Delete group
        </Button>
      </div>
      {problem && <ErrorText>{problem}</ErrorText>}
      <EntryList entries={entries} draft={draft} setDraft={setDraft} />
    </section>
  )
}

/** Entries, each editable, movable to another group, and deletable. */
function EntryList({
  entries,
  draft,
  setDraft,
}: {
  entries: DraftEntry[]
  draft: Draft
  setDraft: (draft: Draft) => void
}) {
  if (entries.length === 0) return <p className="text-sm text-ink-muted">No entries yet.</p>
  return (
    <ul className="flex flex-col divide-y divide-line-soft">
      {entries.map((entry) => (
        <EntryRow key={entry.id} entry={entry} draft={draft} setDraft={setDraft} />
      ))}
    </ul>
  )
}

function EntryRow({
  entry,
  draft,
  setDraft,
}: {
  entry: DraftEntry
  draft: Draft
  setDraft: (draft: Draft) => void
}) {
  const [editing, setEditing] = useState(false)
  const [ball, setBall] = useState(entry.ball)
  const [square, setSquare] = useState(squareOf(entry))
  const [problem, setProblem] = useState<string>()
  const matched = entry.square === null

  if (editing) {
    return (
      <li>
        <form
          className="flex flex-col gap-2 py-2"
          onSubmit={(e) => {
            e.preventDefault()
            const result = editEntry(draft, entry.id, ball, matched ? null : square)
            setProblem(result.problem && describe(result.problem))
            if (!result.problem) {
              setDraft(result.draft)
              setEditing(false)
            }
          }}
        >
          <label className="flex flex-col gap-1 text-sm font-semibold">
            {matched ? 'Word or phrase' : 'What is called'}
            <input
              className={input}
              value={ball}
              onChange={(e) => {
                setBall(e.target.value)
              }}
            />
          </label>
          {!matched && (
            <label className="flex flex-col gap-1 text-sm font-semibold">
              What players mark
              <input
                className={input}
                value={square}
                onChange={(e) => {
                  setSquare(e.target.value)
                }}
              />
            </label>
          )}
          {problem && <ErrorText>{problem}</ErrorText>}
          <div className="flex gap-2">
            <Button type="submit" look="secondary">
              Save
            </Button>
            <Button
              look="quiet"
              onClick={() => {
                setEditing(false)
              }}
            >
              Cancel
            </Button>
          </div>
        </form>
      </li>
    )
  }

  return (
    <li className="flex flex-col gap-1 py-2 sm:flex-row sm:items-center sm:gap-3">
      {/* Phones: the text gets the full width, the controls a row below it. */}
      <span className="min-w-0 flex-1">
        <span className="block break-words">{entry.ball}</span>
        {!matched && (
          <span className="block font-display font-bold break-words">{squareOf(entry)}</span>
        )}
      </span>
      <div className="flex flex-wrap items-center gap-2">
        {draft.groups.length > 0 && (
          <label className="text-sm">
            <span className="sr-only">Group for {entry.ball}</span>
            <select
              className="min-h-12 w-36 rounded-xl border-2 border-line bg-surface px-2 sm:w-auto"
              value={entry.groupId ?? ''}
              onChange={(e) => {
                setDraft(moveEntry(draft, entry.id, e.target.value || null))
              }}
            >
              <option value="">Not in a group</option>
              {draft.groups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <Button
          look="quiet"
          aria-label={`Edit ${entry.ball}`}
          onClick={() => {
            setBall(entry.ball)
            setSquare(squareOf(entry))
            setEditing(true)
          }}
        >
          Edit
        </Button>
        <Button
          look="quiet"
          aria-label={`Delete ${entry.ball}`}
          onClick={() => {
            setDraft(deleteEntry(draft, entry.id))
          }}
        >
          Delete
        </Button>
      </div>
    </li>
  )
}
