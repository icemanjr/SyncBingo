import { ApiError, type Schemas } from '../../api/client'

/**
 * A request to the game's set (`/content`), sent with plain `fetch`: its body is the set file itself, which the
 * typed client would treat as JSON. Throws the API's refusal as an {@link ApiError}.
 */
async function contentRequest(
  code: string,
  token: string,
  init: { method?: string; query?: string; body?: string } = {},
): Promise<Response> {
  const response = await fetch(
    new Request(`${window.location.origin}/api/sessions/${code}/content${init.query ?? ''}`, {
      method: init.method ?? 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
        ...(init.body === undefined ? {} : { 'Content-Type': 'application/json' }),
      },
      body: init.body,
    }),
  )
  if (!response.ok)
    throw ApiError.from(response.status, await response.json().catch(() => undefined))
  return response
}

/** The game's custom set file as text, e.g. to start editing it. */
export async function fetchSetFile(code: string, token: string): Promise<string> {
  return (await contentRequest(code, token)).text()
}

/**
 * Uses a set file in the game (Use this set): checked by the server, then every card is redealt. A set with
 * groups is dealt by group, one per card column. With `cardSize`, onto that card (FR-26a), in the same redeal.
 */
export async function applySetFile(
  code: string,
  token: string,
  file: string,
  cardSize?: Schemas['CardSize'],
): Promise<void> {
  await contentRequest(code, token, {
    method: 'PUT',
    query: cardSize ? `?cardSize=${cardSize}` : '',
    body: file,
  })
}

/** Back to numbers: the standard balls and a new card for everyone. */
export async function backToNumbers(code: string, token: string): Promise<void> {
  await contentRequest(code, token, { method: 'DELETE' })
}

/**
 * Downloads the game's custom set as its file (ADR 0009): the host any time, a player when the host shares
 * it, in the lobby. Saved under the name the server gives it ("Science vocabulary.json").
 */
export async function downloadSet(code: string, token: string): Promise<void> {
  const response = await contentRequest(code, token)

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
