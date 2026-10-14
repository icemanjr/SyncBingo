import { useState, type SyntheticEvent } from 'react'
import { useNavigate } from 'react-router'
import { api, unwrap } from '../../api/client'
import { seats } from '../../storage/sessions'
import { normalizeCode } from '../game/codes'
import { useAction } from '../game/useAction'
import { Button, ErrorText } from '../game/ui'

/** Starting a game (FR-2): the host picks the password players will need; everything else is in Options. */
export function CreateGamePage() {
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const { busy, error, run } = useAction()

  const submit = (e: SyntheticEvent) => {
    e.preventDefault()
    void run(async () => {
      const created = await unwrap(
        api.POST('/api/sessions', { body: { password, settings: null, winCondition: null } }),
      )
      const code = normalizeCode(created.code)
      seats.save(code, { role: 'host', token: created.hostToken })
      await navigate(`/host/${code}`)
    })
  }

  return (
    <form
      onSubmit={submit}
      className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center gap-4"
    >
      <h1 className="text-3xl font-bold">Host a game</h1>
      <div className="flex flex-col gap-1">
        <label htmlFor="game-password" className="font-semibold">
          Game password
        </label>
        <input
          id="game-password"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value)
          }}
          minLength={4}
          maxLength={64}
          required
          autoComplete="off"
          aria-describedby="password-hint"
          className="min-h-12 rounded-xl border-2 border-slate-300 px-3 text-lg focus:border-indigo-600 focus:outline-none"
        />
        <p id="password-hint" className="text-sm text-slate-600">
          Players type this to join. Pick something easy to say out loud, like the event&apos;s name
          (4–64 characters).
        </p>
      </div>
      {error && <ErrorText>{error}</ErrorText>}
      <Button type="submit" disabled={busy}>
        {busy ? 'Creating…' : 'Create game'}
      </Button>
    </form>
  )
}
