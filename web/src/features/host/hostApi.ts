import { api, unwrap, type Schemas } from '../../api/client'

const auth = (token: string) => ({ Authorization: `Bearer ${token}` })

/** The host's commands (docs/05 §3), each one typed from the API description. */
export function hostApi(code: string, token: string) {
  const path = { code }
  const headers = auth(token)
  return {
    startCountdown: (startInAuto: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/rounds/countdown', {
          params: { path },
          headers,
          body: { startInAuto },
        }),
      ),
    cancelCountdown: () =>
      unwrap(api.DELETE('/api/sessions/{code}/rounds/countdown', { params: { path }, headers })),
    grabBall: () => unwrap(api.POST('/api/sessions/{code}/balls', { params: { path }, headers })),
    startAuto: () => unwrap(api.POST('/api/sessions/{code}/auto', { params: { path }, headers })),
    pause: () => unwrap(api.POST('/api/sessions/{code}/pause', { params: { path }, headers })),
    endRound: () =>
      unwrap(api.POST('/api/sessions/{code}/rounds/current/end', { params: { path }, headers })),
    clearCards: (redrawCards: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/cards/clear', {
          params: { path },
          headers,
          body: { redrawCards },
        }),
      ),
    award: (participantId: string, claimId?: number, wins?: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/wins', {
          params: { path, header: { 'Idempotency-Key': crypto.randomUUID() } },
          headers,
          body: { participantId, claimId: claimId ?? null, wins: wins ?? null, cardId: null },
        }),
      ),
    dismiss: (claimId: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/claims/{claimId}/dismiss', {
          params: { path: { code, claimId } },
          headers,
        }),
      ),
    remove: (participantId: string) =>
      unwrap(
        api.DELETE('/api/sessions/{code}/participants/{id}', {
          params: { path: { code, id: participantId } },
          headers,
        }),
      ),
    inspect: (participantId: string) =>
      unwrap(
        api.GET('/api/sessions/{code}/participants/{id}/card', {
          params: { path: { code, id: participantId } },
          headers,
        }),
      ),
    endGame: () => unwrap(api.POST('/api/sessions/{code}/end', { params: { path }, headers })),
    applyRules: (settings: Schemas['SettingsBody'] | null, winCondition: string | null) =>
      unwrap(
        api.PUT('/api/sessions/{code}/rules', {
          params: { path },
          headers,
          body: { settings, winCondition },
        }),
      ),
    showLeaderboard: (visible: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/leaderboard/show', {
          params: { path },
          headers,
          body: { visible },
        }),
      ),
    leaderboard: () =>
      unwrap(api.GET('/api/sessions/{code}/leaderboard', { params: { path }, headers })),
    createDisplayLink: () =>
      unwrap(api.POST('/api/sessions/{code}/display-links', { params: { path }, headers })),
    displays: () => unwrap(api.GET('/api/sessions/{code}/displays', { params: { path }, headers })),
    disconnectDisplay: (displayId: string) =>
      unwrap(
        api.DELETE('/api/sessions/{code}/displays/{id}', {
          params: { path: { code, id: displayId } },
          headers,
        }),
      ),
    connectTv: (pairingCode: string) =>
      unwrap(
        api.POST('/api/sessions/{code}/display-pairings', {
          params: { path },
          headers,
          body: { pairingCode },
        }),
      ),
    setJoinNote: (note: string) =>
      unwrap(
        api.PUT('/api/sessions/{code}/join-note', { params: { path }, headers, body: { note } }),
      ),
    /** "Players can download this set" (ADR 0009). */
    setContentSharing: (playersMayDownload: boolean) =>
      unwrap(
        api.PUT('/api/sessions/{code}/content/sharing', {
          params: { path },
          headers,
          body: { playersMayDownload },
        }),
      ),
    /** "Call board shows" (FR-29): which face of a custom set's items every call board shows. */
    setBoardSide: (side: Schemas['BoardSide']) =>
      unwrap(
        api.PUT('/api/sessions/{code}/board-side', { params: { path }, headers, body: { side } }),
      ),
  }
}

export type HostApi = ReturnType<typeof hostApi>
