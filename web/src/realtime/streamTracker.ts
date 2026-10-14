/** What to do with an incoming event, given the last one applied (ADR 0002, amended). */
export type Decision = 'apply' | 'duplicate' | 'gap'

/**
 * Follows one client's own event stream. Each audience (players, host, display) numbers its events
 * 1, 2, 3… without gaps, so a jump always means a missed event and the client reloads the snapshot,
 * whose `seq` says where the stream continues.
 */
export class StreamTracker {
  #position: number

  constructor(position: number) {
    this.#position = position
  }

  get position(): number {
    return this.#position
  }

  accept(seq: number): Decision {
    if (seq <= this.#position) {
      return 'duplicate'
    }

    if (seq > this.#position + 1) {
      return 'gap'
    }

    this.#position = seq
    return 'apply'
  }

  /** After reloading the snapshot: continue from its position. */
  resetTo(position: number): void {
    this.#position = position
  }
}
