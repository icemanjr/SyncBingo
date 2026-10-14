import { describe, expect, it } from 'vitest'
import { ApiError, unwrap } from './client'

describe('ApiError', () => {
  it('keeps the message players may see and the code for branching', () => {
    const error = ApiError.from(409, {
      detail: 'Pause before changing the rules.',
      code: 'WrongState',
    })

    expect(error.status).toBe(409)
    expect(error.code).toBe('WrongState')
    expect(error.message).toBe('Pause before changing the rules.')
  })

  it('falls back to a friendly message when the body is not a problem', () => {
    expect(ApiError.from(500, undefined).detail).toBe('Something went wrong. Please try again.')
  })
})

describe('unwrap', () => {
  it('returns the data of a success and throws the problem of a refusal', async () => {
    await expect(
      unwrap(Promise.resolve({ data: 1, response: new Response(null, { status: 200 }) })),
    ).resolves.toBe(1)
    await expect(
      unwrap(
        Promise.resolve({
          error: { detail: 'Too many attempts.', code: 'TooManyRequests' },
          response: new Response(null, { status: 429 }),
        }),
      ),
    ).rejects.toMatchObject({ status: 429, code: 'TooManyRequests' })
  })
})
