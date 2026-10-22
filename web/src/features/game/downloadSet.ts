import { ApiError } from '../../api/client'

/** The game's custom set file, or the API's refusal. */
async function fetchSet(code: string, token: string): Promise<Response> {
  const response = await fetch(
    new Request(`${window.location.origin}/api/sessions/${code}/content`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  if (!response.ok)
    throw ApiError.from(response.status, await response.json().catch(() => undefined))
  return response
}

/** The game's custom set file as text, e.g. to start editing it. */
export async function fetchSetFile(code: string, token: string): Promise<string> {
  return (await fetchSet(code, token)).text()
}

/**
 * Uses a set file in the game (Use this set): checked by the server, then every card is redealt. A set with
 * groups is dealt by group, one per card column.
 */
export async function applySetFile(code: string, token: string, file: string): Promise<void> {
  const response = await fetch(
    new Request(`${window.location.origin}/api/sessions/${code}/content`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: file,
    }),
  )
  if (!response.ok)
    throw ApiError.from(response.status, await response.json().catch(() => undefined))
}

/** Back to numbers: the standard balls and a new card for everyone. */
export async function backToNumbers(code: string, token: string): Promise<void> {
  const response = await fetch(
    new Request(`${window.location.origin}/api/sessions/${code}/content`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  if (!response.ok)
    throw ApiError.from(response.status, await response.json().catch(() => undefined))
}

/**
 * Downloads the game's custom set as its file (ADR 0009): the host any time, a player when the host shares
 * it, in the lobby. Saved under the name the server gives it ("Science vocabulary.json").
 */
export async function downloadSet(code: string, token: string): Promise<void> {
  const response = await fetchSet(code, token)

  const disposition = response.headers.get('Content-Disposition') ?? ''
  const named = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1]
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1]
  const name = named ? decodeURIComponent(named) : (plain ?? 'SyncBingo set.json')

  saveFile(name, await response.blob())
}

/** Saves text or a blob as a file on this device, e.g. the editor's draft. */
export function saveFile(name: string, content: Blob | string): void {
  const blob =
    typeof content === 'string' ? new Blob([content], { type: 'application/json' }) : content
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}
