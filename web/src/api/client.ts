import createClient, { type Middleware } from 'openapi-fetch'
import type { components, paths } from './schema'

/** Types generated from the API's own OpenAPI description (`npm run api:generate`). */
export type Schemas = components['schemas']

/** The API, typed end to end. Paths are relative, so the dev proxy and production serve it from the same origin. */
export const api = createClient<paths>({ baseUrl: '' })

/**
 * A refusal from the API (RFC 7807, docs/05 §5): `detail` is safe to show to players, `code` names the
 * reason (`WrongState`, `NotAllowed`, `TooManyRequests`…) for code to branch on.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly detail: string

  constructor(status: number, code: string | undefined, detail: string) {
    super(detail)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.detail = detail
  }

  static from(status: number, body: unknown): ApiError {
    const problem = (body ?? {}) as { detail?: unknown; code?: unknown }
    return new ApiError(
      status,
      typeof problem.code === 'string' ? problem.code : undefined,
      typeof problem.detail === 'string'
        ? problem.detail
        : 'Something went wrong. Please try again.',
    )
  }
}

/** Sends the caller's session token (ADR 0005) on every request made through `client`. */
export function withToken(token: string): Middleware {
  return {
    onRequest({ request }) {
      request.headers.set('Authorization', `Bearer ${token}`)
      return request
    },
  }
}

/** Unwraps an openapi-fetch result: the data, or an {@link ApiError}. */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call
  if (!response.ok) {
    throw ApiError.from(response.status, error)
  }

  return data as T
}
