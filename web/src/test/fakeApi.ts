import { vi } from 'vitest'

export interface Sent {
  method: string
  path: string
  body: unknown
  headers: Headers
}

type Reply = { status?: number; body?: unknown } | undefined

/**
 * Stands in for the API: answers `fetch` by "METHOD /path" (path parameters as written in the request),
 * records every request, and answers 204 for anything unrouted.
 */
export function fakeApi(routes: Record<string, Reply | ((sent: Sent) => Reply)>) {
  const sent: Sent[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = new URL(input.url)
      const text = await input.text()
      const request: Sent = {
        method: input.method,
        path: decodeURIComponent(url.pathname),
        body: text ? (JSON.parse(text) as unknown) : undefined,
        headers: input.headers,
      }
      sent.push(request)
      const route = routes[`${request.method} ${request.path}`]
      const reply = typeof route === 'function' ? route(request) : route
      const status = reply?.status ?? (reply?.body === undefined ? 204 : 200)
      return new Response(reply?.body === undefined ? null : JSON.stringify(reply.body), {
        status,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
  return { sent }
}
