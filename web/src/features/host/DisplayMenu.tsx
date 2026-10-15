import { useEffect, useState } from 'react'
import type { Schemas } from '../../api/client'
import { useAction } from '../game/useAction'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

/**
 * **Open display ⧉** (docs/06): read-only screens for the room (FR-28). Open one in a new tab or copy its
 * link, connect a smart TV with the code it shows (ADR 0010), see and disconnect displays, and set the note
 * shown under the join QR (FR-49). Not rules, so it works in auto mode too.
 */
export function DisplayMenu({
  host,
  joinNote,
  onClose,
}: {
  host: HostApi
  joinNote: string | null
  onClose: () => void
}) {
  const [displays, setDisplays] = useState<Schemas['DisplayRecord'][]>([])
  const [link, setLink] = useState<string>()
  const [tvCode, setTvCode] = useState('')
  const [note, setNote] = useState(joinNote ?? '')
  const [message, setMessage] = useState<string>()
  const { busy, error, run } = useAction()

  const reload = () =>
    run(async () => {
      setDisplays(await host.displays())
    })
  useEffect(() => {
    void reload()
    // Once, when the menu opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const newLink = async () => {
    const created = await host.createDisplayLink()
    await reload()
    return created.displayUrl
  }

  return (
    <Panel title="Displays">
      <div className="flex flex-col gap-5">
        <div className="flex flex-wrap gap-2">
          <Button
            disabled={busy}
            onClick={() =>
              void run(async () => {
                window.open(await newLink(), '_blank', 'noopener')
              })
            }
          >
            Open in a new tab
          </Button>
          <Button
            look="secondary"
            disabled={busy}
            onClick={() =>
              void run(async () => {
                const url = await newLink()
                setLink(url)
                try {
                  await navigator.clipboard.writeText(url)
                  setMessage('Display link copied.')
                } catch {
                  setMessage('Copy this link to the other device:')
                }
              })
            }
          >
            Copy display link
          </Button>
        </div>
        {message && <p className="text-sm text-ink-muted">{message}</p>}
        {link && <p className="text-sm break-all">{link}</p>}

        <form
          className="flex flex-col gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            void run(async () => {
              await host.connectTv(tvCode)
              setTvCode('')
              setMessage('TV connected.')
              await reload()
            })
          }}
        >
          <label htmlFor="tv-code" className="font-semibold">
            Connect a TV
          </label>
          <p id="tv-code-help" className="text-sm text-ink-muted">
            On the TV, open syncbingo.com/tv and type the code it shows.
          </p>
          <div className="flex flex-wrap gap-2">
            <input
              id="tv-code"
              value={tvCode}
              onChange={(e) => {
                setTvCode(e.target.value)
              }}
              placeholder="K7M-2QX"
              autoComplete="off"
              autoCapitalize="characters"
              aria-describedby="tv-code-help"
              className="min-h-12 w-36 rounded-xl border-2 border-line bg-surface px-3 font-mono text-lg uppercase"
            />
            <Button type="submit" look="secondary" disabled={busy || tvCode.trim().length < 6}>
              Connect
            </Button>
          </div>
        </form>

        <form
          className="flex flex-col gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            void run(async () => {
              await host.setJoinNote(note)
              setMessage(note.trim() ? 'Shown with the join QR.' : 'Note removed.')
            })
          }}
        >
          <label htmlFor="join-note" className="font-semibold">
            Password note
          </label>
          <p id="join-note-help" className="text-sm text-ink-muted">
            Shown with the join QR on displays exactly as you type it, such as the password or a
            hint. Never on phones. Leave it empty to show nothing.
          </p>
          <div className="flex flex-wrap gap-2">
            <input
              id="join-note"
              value={note}
              maxLength={80}
              onChange={(e) => {
                setNote(e.target.value)
              }}
              placeholder="Password: harvest2026"
              aria-describedby="join-note-help"
              className="min-h-12 flex-1 rounded-xl border-2 border-line bg-surface px-3"
            />
            <Button type="submit" look="secondary" disabled={busy}>
              Save
            </Button>
          </div>
        </form>

        <div>
          <h3 className="font-semibold">Connected ({displays.length})</h3>
          <ul className="flex flex-col divide-y divide-line-soft">
            {displays.map((display, i) => (
              <li key={display.displayId} className="flex items-center justify-between py-1">
                <span>
                  {display.kind === 'Tv' ? 'TV' : 'Display link'} {i + 1}
                </span>
                <Button
                  look="quiet"
                  disabled={busy}
                  onClick={() =>
                    void run(async () => {
                      await host.disconnectDisplay(display.displayId)
                      await reload()
                    })
                  }
                >
                  Disconnect
                </Button>
              </li>
            ))}
          </ul>
        </div>

        {error && <ErrorText>{error}</ErrorText>}
        <Button look="quiet" onClick={onClose}>
          Close
        </Button>
      </div>
    </Panel>
  )
}
