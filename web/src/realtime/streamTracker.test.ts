import { describe, expect, it } from 'vitest'
import { StreamTracker } from './streamTracker'

describe('StreamTracker', () => {
  it('applies the next event in the stream', () => {
    const tracker = new StreamTracker(4)

    expect(tracker.accept(5)).toBe('apply')
    expect(tracker.position).toBe(5)
  })

  it('ignores an event it has already applied', () => {
    const tracker = new StreamTracker(4)

    expect(tracker.accept(4)).toBe('duplicate')
    expect(tracker.accept(2)).toBe('duplicate')
    expect(tracker.position).toBe(4)
  })

  it('reports a gap without moving, so the snapshot decides where to carry on', () => {
    const tracker = new StreamTracker(4)

    expect(tracker.accept(6)).toBe('gap')
    expect(tracker.position).toBe(4)

    tracker.resetTo(6)
    expect(tracker.accept(7)).toBe('apply')
  })
})
