import { afterEach, describe, expect, it, vi } from 'vitest'
import { seats } from './sessions'

describe('seats', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
  })

  it('remembers how this browser takes part in a game', () => {
    seats.save('abc', { role: 'player', token: 't', participantId: 'p1' })

    expect(seats.load('abc')).toEqual({ role: 'player', token: 't', participantId: 'p1' })
    seats.forget('abc')
    expect(seats.load('abc')).toBeUndefined()
  })

  it('keeps working when storage is blocked', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(() => {
      seats.save('abc', { role: 'host', token: 't' })
    }).not.toThrow()
    expect(seats.load('abc')).toBeUndefined()
  })
})
