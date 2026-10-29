import { useState } from 'react'
import { displayCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'
import type { HostApi } from './hostApi'
import { useJoinQr } from './useJoinQr'

/**
 * The lobby's join panel on the host console (docs/06, the design reference): **Join Now!**, the QR with
 * the game code under it and room around it for phone cameras, how joining works, and the password note
 * (FR-49): labeled and edited in place for the host; on the TV (no `host`) shown exactly as typed, with
 * no label, and not at all when empty. Never on the join page, so a mistyped code gets no hint.
 */
export function JoinStage({
  code,
  joinNote,
  host,
}: {
  code: string
  joinNote: string | null
  /** Without it the panel is read-only, as on the TV display: the note shows but can't be edited. */
  host?: HostApi
}) {
  const { qr } = useJoinQr(code)
  const [editing, setEditing] = useState(false)
  const [note, setNote] = useState(joinNote ?? '')
  const { busy, error, run } = useAction()

  return (
    <section
      aria-label="How to join"
      className="flex flex-col gap-10 rounded-3xl bg-[#1c1633] p-6 text-white sm:p-8 dark:bg-[#2a2245]"
    >
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
        <h2 className="font-display text-5xl font-extrabold tracking-tight text-ball">Join Now!</h2>
        <p className="font-display text-2xl leading-tight font-bold sm:text-right">
          Scan or visit
          <br />
          {window.location.host}
        </p>
      </div>

      <div className="flex flex-col items-center gap-7 sm:flex-row">
        <div className="flex shrink-0 flex-col items-center gap-3">
          <div className="flex h-60 w-60 items-center justify-center rounded-2xl bg-white p-3">
            {qr && (
              <img
                src={qr}
                alt={`QR code to join game ${displayCode(code)}`}
                className="h-full w-full"
              />
            )}
          </div>
          <div className="text-center">
            <p className="text-xs font-bold tracking-widest text-[#c9bfff] uppercase">Game code</p>
            <p className="font-display text-xl font-bold tracking-wider whitespace-nowrap uppercase">
              {displayCode(code)}
            </p>
          </div>
        </div>
        <p className="text-lg leading-relaxed text-[#d9d3ea]">
          Players scan the QR with their device’s camera, or type the game code at{' '}
          {window.location.host}, then the password you give them.
          <br />
          <br />
          Anyone who arrives during a round waits, and gets a card as soon as it ends.
        </p>
      </div>

      {editing ? (
        <form
          className="flex flex-wrap items-center gap-2 rounded-2xl bg-white/10 p-3"
          onSubmit={(e) => {
            e.preventDefault()
            void run(async () => {
              await host?.setJoinNote(note)
              setEditing(false)
            })
          }}
        >
          <label htmlFor="stage-join-note" className="sr-only">
            Password note
          </label>
          <input
            id="stage-join-note"
            value={note}
            maxLength={80}
            onChange={(e) => {
              setNote(e.target.value)
            }}
            placeholder="e.g. The password is the dog's name"
            className="min-h-12 min-w-0 flex-1 rounded-xl bg-white px-3 text-[#1c1633]"
          />
          <Button type="submit" disabled={busy}>
            Save
          </Button>
          <Button
            look="quiet"
            className="text-white"
            onClick={() => {
              setNote(joinNote ?? '')
              setEditing(false)
            }}
          >
            Cancel
          </Button>
        </form>
      ) : host ? (
        <div className="flex items-center gap-3 rounded-2xl bg-white/10 px-4 py-3">
          <svg
            aria-hidden="true"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="#ffc23d"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <rect x="3" y="11" width="18" height="11" rx="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
          <p className="flex-1">
            {joinNote ? (
              <>
                Password note: <b>{joinNote}</b>
              </>
            ) : (
              'No password note. Add the password, or a hint, to show it on the TV.'
            )}
          </p>
          <button
            type="button"
            onClick={() => {
              setEditing(true)
            }}
            className="min-h-12 rounded-xl border border-white/40 px-4 font-bold focus-visible:outline-4 focus-visible:outline-white"
          >
            {joinNote ? 'Edit' : 'Add'}
          </button>
        </div>
      ) : (
        // On the TV: exactly what the host typed, no label (it may simply be the password); nothing if empty.
        joinNote && (
          <p className="rounded-2xl bg-white/10 px-5 py-4 text-center font-display text-3xl font-bold">
            {joinNote}
          </p>
        )
      )}
      {error && <ErrorText>{error}</ErrorText>}
    </section>
  )
}
