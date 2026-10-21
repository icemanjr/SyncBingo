import { ApiError } from '../../api/client'

/**
 * Downloads the game's custom set as its file (ADR 0009): the host any time, a player when the host shares
 * it, in the lobby. Saved under the name the server gives it ("Science vocabulary.json").
 */
export async function downloadSet(code: string, token: string): Promise<void> {
  const response = await fetch(`/api/sessions/${code}/content`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  if (!response.ok)
    throw ApiError.from(response.status, await response.json().catch(() => undefined))

  const disposition = response.headers.get('Content-Disposition') ?? ''
  const named = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1]
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1]
  const name = named ? decodeURIComponent(named) : (plain ?? 'SyncBingo set.json')

  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}
