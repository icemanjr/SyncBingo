import { describe, expect, it } from 'vitest'
import { aCall, aHostSnapshot } from '../../test/fixtures'
import { applyEvent } from './gameEvents'

const event = (type: string, data: unknown = {}, seq = 1) => ({ type, seq, data })

describe('applyEvent', () => {
  it('adds a called ball and counts one fewer left', () => {
    const before = aHostSnapshot({ state: 'Manual', calls: [], ballsRemaining: 75 })

    const { snapshot, refetch } = applyEvent(
      before,
      event('BallCalled', { call: aCall(7), previousNumber: null }),
    )

    expect(snapshot.game.calls).toEqual([aCall(7)])
    expect(snapshot.game.ballsRemaining).toBe(74)
    expect(refetch).toBe(false)
  })

  it('keeps calls hidden from a player who may not see them', () => {
    const before = aHostSnapshot({ state: 'Manual', calls: null })

    expect(
      applyEvent(before, event('BallCalled', { call: aCall(7) })).snapshot.game.calls,
    ).toBeNull()
  })

  it('follows the countdown into a fresh round', () => {
    const lobby = aHostSnapshot({ calls: [aCall(3)], ballsRemaining: 74 })

    const counting = applyEvent(
      lobby,
      event('RoundCountdownStarted', {
        round: 1,
        endsAt: '2026-10-14T19:00:10Z',
        winConditionId: 'blackout',
      }),
    )
    expect(counting.snapshot.game).toMatchObject({
      state: 'Starting',
      countdownEndsAt: '2026-10-14T19:00:10Z',
      winCondition: 'blackout',
    })

    const started = applyEvent(counting.snapshot, event('RoundStarted', { round: 1 }))
    expect(started.snapshot.game).toMatchObject({
      state: 'Manual',
      round: 1,
      calls: [],
      ballsRemaining: 75,
      countdownEndsAt: null,
    })
  })

  it('switches between manual and auto for the host', () => {
    const manual = aHostSnapshot({ state: 'Manual' })

    const auto = applyEvent(manual, event('AutoCallingStarted')).snapshot
    expect(auto.game.state).toBe('Auto')
    expect(applyEvent(auto, event('AutoCallingPaused')).snapshot.game.state).toBe('Manual')
  })

  it('raises the rules notice and the reading-time wait', () => {
    const { snapshot, rulesNotice } = applyEvent(
      aHostSnapshot({ state: 'Manual' }),
      event('RulesUpdated', {
        changes: [{ rule: 'winCondition', value: 'blackout' }],
        nextBallFrom: '2026-10-14T19:00:03Z',
      }),
    )

    expect(snapshot.game.nextBallFrom).toBe('2026-10-14T19:00:03Z')
    expect(rulesNotice?.changes).toEqual([{ rule: 'winCondition', value: 'blackout' }])
  })

  it('asks for the snapshot after claims, wins and removals, and alerts the host to wins', () => {
    const before = aHostSnapshot({ state: 'Manual' })

    expect(applyEvent(before, event('WinClaimed')).refetch).toBe(true)
    expect(applyEvent(before, event('ParticipantLeft', { name: 'Ben' })).alert).toEqual({
      type: 'ParticipantLeft',
      data: { name: 'Ben' },
    })
    expect(applyEvent(before, event('ParticipantRemoved', { name: 'Ben' })).alert).toEqual({
      type: 'ParticipantRemoved',
      data: { name: 'Ben' },
    })
    expect(applyEvent(before, event('WinDetected', { name: 'Ava' })).alert).toEqual({
      type: 'WinDetected',
      data: { name: 'Ava' },
    })
  })

  it('asks for the snapshot after an event it does not know', () => {
    expect(applyEvent(aHostSnapshot(), event('SomethingNew')).refetch).toBe(true)
  })
})
