/** True when the server no longer knows the game, or this device's place in it: an expired game. */
export const isGone = (error: { status: number } | undefined) =>
  error?.status === 401 || error?.status === 404
