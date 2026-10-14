import { useEffect, useState, type SyntheticEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { api, unwrap } from '../../api/client'
import { seats } from '../../storage/sessions'
import { displayCode, normalizeCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'

const inputClass =
  'min-h-12 rounded-xl border-2 border-line bg-surface px-3 text-lg focus:border-accent focus:outline-none'

/**
 * Joining (FR-3, FR-4): the QR opens this page with the code filled in; players add the password the host
 * shares and a name. Joining opens between rounds only (FR-5). A browser that already joined goes back in.
 */
export function JoinPage() {
  const navigate = useNavigate()
  const fromLink = useParams().code ?? ''
  const [code, setCode] = useState(fromLink ? displayCode(fromLink) : '')
  const [password, setPassword] = useState('')
  const [name, setName] = useState('')
  // What join-info said, and for which code, so a newly typed code never shows an old answer.
  const [info, setInfo] = useState<{ code: string; text: string | undefined }>()
  const { busy, error, run } = useAction()
  const normalized = normalizeCode(code)
  const seat = seats.load(normalized)

  useEffect(() => {
    if (normalized.length !== 16) return
    api
      .GET('/api/sessions/{code}/join-info', { params: { path: { code: normalized } } })
      .then(({ data, response }) => {
        setInfo({
          code: normalized,
          text:
            !response.ok || !data
              ? "That game doesn't exist or has ended. Check the code with your host."
              : data.acceptingJoins
                ? undefined
                : 'A round is in progress. You can join between rounds.',
        })
      })
      .catch(() => {
        setInfo(undefined)
      })
  }, [normalized])
  const status = info?.code === normalized ? info.text : undefined

  const submit = (e: SyntheticEvent) => {
    e.preventDefault()
    void run(async () => {
      const joined = await unwrap(
        api.POST('/api/sessions/{code}/join', {
          params: { path: { code: normalized } },
          body: { password, name },
        }),
      )
      seats.save(normalized, {
        role: 'player',
        token: joined.token,
        participantId: joined.participantId,
        name: joined.snapshot.you?.name ?? name,
      })
      await navigate(`/play/${normalized}`)
    })
  }

  if (seat?.role === 'player') {
    return (
      <div className="m-auto flex flex-col items-center gap-3 text-center">
        <p>You're in this game as {seat.name}.</p>
        <Link
          to={`/play/${normalized}`}
          className="flex min-h-12 items-center rounded-xl bg-accent px-6 font-semibold text-accent-ink"
        >
          Back to my card
        </Link>
      </div>
    )
  }

  return (
    <form
      onSubmit={submit}
      className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4"
    >
      <h1 className="text-3xl font-bold">Join a game</h1>
      <div className="flex flex-col gap-1">
        <label htmlFor="join-code" className="font-semibold">
          Game code
        </label>
        <input
          id="join-code"
          value={code}
          onChange={(e) => {
            setCode(e.target.value)
          }}
          required
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          placeholder="xxxx-xxxx-xxxx-xxxx"
          className={`${inputClass} font-mono`}
        />
      </div>
      <div className="flex flex-col gap-1">
        <label htmlFor="join-password" className="font-semibold">
          Game password
        </label>
        <input
          id="join-password"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value)
          }}
          required
          autoComplete="off"
          className={inputClass}
        />
      </div>
      <div className="flex flex-col gap-1">
        <label htmlFor="join-name" className="font-semibold">
          Your name
        </label>
        <input
          id="join-name"
          value={name}
          maxLength={20}
          onChange={(e) => {
            setName(e.target.value)
          }}
          required
          autoComplete="nickname"
          className={inputClass}
        />
      </div>
      {status && <p className="font-medium text-ink-muted">{status}</p>}
      {error && <ErrorText>{error}</ErrorText>}
      <Button type="submit" disabled={busy}>
        {busy ? 'Joining…' : 'Join'}
      </Button>
    </form>
  )
}
